using System.Text.Json;
using Microsoft.Extensions.Logging;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Cloud;

/// <summary>
///     Recebe um lote de um servidor de jogo (plano §5): já gravado no diário
///     dele, então pode chegar repetido, atrasado ou de um servidor que perdeu o
///     lease. Três respostas possíveis:
///     <list type="bullet">
///         <item>"applied": gravado agora, numa transação (saldos, ledger, lease);</item>
///         <item>"duplicate": já tinha sido aplicado (reenvio após queda);</item>
///         <item>"quarantined": recusado e guardado para o dono decidir; os canais tocados congelam.</item>
///     </list>
///     Conflito de concorrência (dois lotes do mesmo jogador ao mesmo tempo) não
///     é resposta: nada foi gravado e o servidor de jogo reenvia.
/// </summary>
public sealed partial class SubmitCloudBatch(
    ICloudStorageRepository storage,
    ICloudCredentialRepository credentials,
    ILogger<SubmitCloudBatch> logger)
{
    public async Task<CloudCallResult<CloudBatchReply>> HandleAsync(CloudServerContext ctx, CloudBatchRequest request,
        CancellationToken ct)
    {
        var player = CloudChannel.NormalizePlayerUuid(request.PlayerUuid);
        if (player is null || request.Epoch < 1 || request.Seq < 1)
            return CloudCallResult<CloudBatchReply>.Invalid("Jogador, época ou sequência inválidos.");

        var hash = CloudBatchHash.Of(request);
        var existing = await storage.FindBatchAsync(ctx.VaultId, player, request.Epoch, request.Seq, ct);
        if (existing is not null)
            return Repeated(existing, hash, ctx, player, request);

        var vault = await credentials.GetVaultAsync(ctx.VaultId, ct);
        if (vault is null)
            return CloudCallResult<CloudBatchReply>.Forbidden("Nuvem não encontrada.");

        var channels = (await storage.ListChannelsAsync(ctx.VaultId, player, ct)).ToDictionary(c => c.Id);
        var lease = await storage.GetLeaseAsync(ctx.VaultId, player, ct);
        var check = lease?.Check(ctx.ServerId, request.Epoch, request.Seq) ?? CloudBatchCheck.UnknownEpoch;

        switch (check)
        {
            case CloudBatchCheck.Duplicate:
                // Seq já aplicada sem registro do lote: não deveria existir (todo
                // lote aplicado é registrado), mas reaplicar seria duplicar.
                return CloudCallResult<CloudBatchReply>.Ok(new CloudBatchReply("duplicate", null));
            case CloudBatchCheck.Accept:
                break;
            default:
                return await Quarantine(ctx, player, request, hash, ReasonOf(check),
                    $"Lease recusou o lote: {check} (lease na época {lease?.Epoch ?? 0}, seq {lease?.LastSeq ?? 0}).",
                    channels.Keys, ct);
        }

        var state = await LoadState(request, channels, vault, ct);
        var outcome = CloudBatchDecision.Decide(request, state);
        if (outcome is CloudBatchDecision.Rejected rejected)
        {
            return await Quarantine(ctx, player, request, hash, rejected.Reason, rejected.Detail,
                rejected.Channels.Where(channels.ContainsKey), ct);
        }

        var accepted = (CloudBatchDecision.Accepted)outcome;
        var newItems = accepted.NewItems.Select(CloudItemTypes.FromDto).ToList();
        var itemIds = new Dictionary<string, Guid>(state.ItemIds, StringComparer.Ordinal);
        foreach (var item in newItems)
            itemIds[item.Fingerprint] = item.Id;

        var batch = NewBatch(ctx, player, request, hash);
        var changes = accepted.Changes
            .Select(c => new CloudBalanceChange(c.ChannelId, itemIds[c.Fingerprint], c.Delta, c.After))
            .ToList();
        var readVersion = lease!.Version;
        lease.Advance(request.Seq);

        if (!await storage.CommitAppliedAsync(lease, readVersion, batch, newItems, changes, ct))
            return CloudCallResult<CloudBatchReply>.Conflict("Outro lote do mesmo jogador chegou junto; reenvie.");

        return CloudCallResult<CloudBatchReply>.Ok(new CloudBatchReply("applied", null));
    }

    private async Task<CloudBatchDecision.State> LoadState(CloudBatchRequest request,
        IReadOnlyDictionary<Guid, CloudChannel> channels, CloudVault vault, CancellationToken ct)
    {
        var balances = await storage.ListBalancesAsync(channels.Keys.ToArray(), ct);
        var itemIds = await storage.ItemIdsByFingerprintAsync(
            request.Ops.Select(o => o.Fingerprint).Distinct(StringComparer.Ordinal).ToArray(), ct);

        // Os saldos chegam por id de item; a decisão fala em impressão digital.
        var balanceItemIds = balances.Select(b => b.ItemTypeId).ToHashSet();
        var fingerprintOf = await storage.FingerprintsByIdAsync(balanceItemIds, ct);

        return new CloudBatchDecision.State(
            channels,
            itemIds,
            balances.ToDictionary(b => (b.ChannelId, fingerprintOf[b.ItemTypeId]), b => b.Amount),
            vault.MaxTypesPerChannel,
            vault.MaxTotalPerChannel,
            vault.MaxItemBytes);
    }

    private CloudCallResult<CloudBatchReply> Repeated(CloudBatch existing, string hash, CloudServerContext ctx,
        string player, CloudBatchRequest request)
    {
        if (existing.PayloadHash != hash)
        {
            // Mesmo número, conteúdo diferente: não há linha nova possível (o
            // índice é único). Responde quarentena para o mod travar o canal.
            LogMismatch(ctx.ServerId, player, request.Epoch, request.Seq);
            return CloudCallResult<CloudBatchReply>.Ok(new CloudBatchReply("quarantined", nameof(CloudQuarantineReason.PayloadMismatch)));
        }

        return existing.Status == CloudBatchStatus.Quarantined
            ? CloudCallResult<CloudBatchReply>.Ok(new CloudBatchReply("quarantined", null))
            : CloudCallResult<CloudBatchReply>.Ok(new CloudBatchReply("duplicate", null));
    }

    private async Task<CloudCallResult<CloudBatchReply>> Quarantine(CloudServerContext ctx, string player,
        CloudBatchRequest request, string hash, CloudQuarantineReason reason, string detail,
        IEnumerable<Guid> channelsToFreeze, CancellationToken ct)
    {
        var batch = NewBatch(ctx, player, request, hash);
        batch.MarkQuarantined();
        var quarantine = new CloudQuarantine
        {
            VaultId = ctx.VaultId,
            BatchId = batch.Id,
            Reason = reason,
            Detail = detail.Length > 1024 ? detail[..1024] : detail,
            PayloadJson = JsonSerializer.Serialize(request)
        };

        try
        {
            await storage.CommitQuarantineAsync(batch, quarantine, channelsToFreeze.ToArray(), ct);
        }
        catch (CloudConcurrencyException)
        {
            return CloudCallResult<CloudBatchReply>.Conflict("O mesmo lote chegou duas vezes ao mesmo tempo; reenvie.");
        }

        LogQuarantined(ctx.ServerId, player, request.Epoch, request.Seq, reason, detail);
        return CloudCallResult<CloudBatchReply>.Ok(new CloudBatchReply("quarantined", reason.ToString()));
    }

    private static CloudBatch NewBatch(CloudServerContext ctx, string player, CloudBatchRequest request, string hash) => new()
    {
        VaultId = ctx.VaultId,
        PlayerUuid = player,
        ServerId = ctx.ServerId,
        Epoch = request.Epoch,
        Seq = request.Seq,
        PayloadHash = hash
    };

    private static CloudQuarantineReason ReasonOf(CloudBatchCheck check) => check switch
    {
        CloudBatchCheck.StaleEpoch => CloudQuarantineReason.StaleEpoch,
        CloudBatchCheck.NotHolder => CloudQuarantineReason.NotHolder,
        CloudBatchCheck.Gap => CloudQuarantineReason.SequenceGap,
        _ => CloudQuarantineReason.UnknownEpoch
    };

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Nuvem: lote {Epoch}/{Seq} de {Player} (servidor {ServerId}) foi para a quarentena: {Reason} — {Detail}")]
    private partial void LogQuarantined(Guid serverId, string player, long epoch, long seq, CloudQuarantineReason reason,
        string detail);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Nuvem: lote {Epoch}/{Seq} de {Player} (servidor {ServerId}) repetiu o número com conteúdo diferente.")]
    private partial void LogMismatch(Guid serverId, string player, long epoch, long seq);
}

/// <summary>A gravação perdeu uma corrida (índice único ou versão do lease): nada foi gravado.</summary>
public sealed class CloudConcurrencyException(string message, Exception? inner = null) : Exception(message, inner);
