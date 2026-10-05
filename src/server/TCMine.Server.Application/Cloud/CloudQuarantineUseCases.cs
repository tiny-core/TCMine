using System.Text.Json;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Cloud;

public sealed class ListCloudQuarantine(ICloudAdminRepository repo, ICloudGovernanceRepository governance, ICurrentUserScope scope)
{
    public async Task<Result<IReadOnlyList<CloudQuarantineView>>> HandleAsync(Guid vaultId, bool openOnly, CancellationToken ct)
    {
        var access = await CloudVaultAccess.RequireAsync(repo, scope, vaultId, ct);
        return access.Succeeded
            ? Result<IReadOnlyList<CloudQuarantineView>>.Success(await governance.ListQuarantineAsync(vaultId, openOnly, ct))
            : Result<IReadOnlyList<CloudQuarantineView>>.Fail(access.Error!);
    }
}

/// <summary>
///     Decide um lote em quarentena. Descartar: o lote nunca entra (os itens que
///     o servidor achava ter guardado se perdem — é para lote inventado ou
///     duplicado). Aplicar: os saldos mudam como o lote dizia, com origem
///     "QuarantineApply" no ledger, contra o estado ATUAL (a decisão reconfere
///     canal, item e saldo negativo). Os canais congelados pelo lote não são
///     descongelados aqui: o dono faz isso na aba Jogadores, depois de olhar.
/// </summary>
public sealed class ResolveCloudQuarantine(
    ICloudAdminRepository repo,
    ICloudGovernanceRepository governance,
    ICloudStorageRepository storage,
    ICurrentUserScope scope,
    TimeProvider clock)
{
    public async Task<Result> HandleAsync(Guid vaultId, Guid quarantineId, bool apply, CancellationToken ct)
    {
        var access = await CloudVaultAccess.RequireAsync(repo, scope, vaultId, ct);
        if (!access.Succeeded)
            return Result.Fail(access.Error!);

        var view = await governance.GetQuarantineAsync(quarantineId, ct);
        if (view is null || view.Quarantine.VaultId != vaultId)
            return Result.Fail("Quarentena não encontrada.");
        if (!view.Quarantine.IsOpen)
            return Result.Fail("Esta quarentena já foi resolvida.");

        var (quarantine, batch) = view;
        var userId = scope.UserId!.Value;
        var now = clock.GetUtcNow();

        if (!apply)
        {
            quarantine.Resolve(CloudQuarantineResolution.Discarded, userId, now);
            batch.MarkResolved(CloudBatchStatus.Discarded);
            await governance.UpdateQuarantineAsync(quarantine, batch, ct);
            await CloudAudit.WriteAsync(governance, vaultId, userId, "quarantine.discard",
                $"Lote {batch.Epoch}/{batch.Seq} de {batch.PlayerUuid}: {quarantine.Reason}", ct);
            return Result.Success();
        }

        CloudBatchRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<CloudBatchRequest>(quarantine.PayloadJson);
        }
        catch (JsonException)
        {
            request = null;
        }
        if (request?.Ops is null || request.Expected is null)
            return Result.Fail("O lote guardado está ilegível; só dá para descartar.");

        var (leases, leaseError) = await CloudLeaseGuard.LoadFreeAsync(storage, vaultId, [batch.PlayerUuid], now, ct);
        if (leases is null)
            return Result.Fail(leaseError!);

        var channels = (await storage.ListChannelsAsync(vaultId, batch.PlayerUuid, ct)).ToDictionary(c => c.Id);
        var balances = await storage.ListBalancesAsync(channels.Keys.ToArray(), ct);
        var fingerprints = await storage.FingerprintsByIdAsync(balances.Select(b => b.ItemTypeId).ToHashSet(), ct);
        var itemIds = await storage.ItemIdsByFingerprintAsync(
            request.Ops.Select(o => o.Fingerprint).Distinct(StringComparer.Ordinal).ToArray(), ct);
        var vault = access.Value!;
        var state = new CloudBatchDecision.State(channels, itemIds,
            balances.ToDictionary(b => (b.ChannelId, fingerprints[b.ItemTypeId]), b => b.Amount),
            vault.MaxTypesPerChannel, vault.MaxTotalPerChannel, vault.MaxItemBytes);

        var outcome = CloudBatchDecision.Decide(request, state, ownerApproval: true);
        if (outcome is CloudBatchDecision.Rejected rejected)
            return Result.Fail($"Não dá para aplicar agora: {rejected.Detail}");

        var accepted = (CloudBatchDecision.Accepted)outcome;
        var newItems = accepted.NewItems.Select(CloudItemTypes.FromDto).ToList();
        var ids = new Dictionary<string, Guid>(itemIds, StringComparer.Ordinal);
        newItems.ForEach(i => ids[i.Fingerprint] = i.Id);

        quarantine.Resolve(CloudQuarantineResolution.Applied, userId, now);
        batch.MarkResolved(CloudBatchStatus.Applied);
        var reason = $"Quarentena aplicada pelo dono (lote {batch.Epoch}/{batch.Seq}, {quarantine.Reason})";
        var committed = await storage.CommitAdminAsync(new CloudAdminCommit(leases, newItems,
            [.. accepted.Changes.Select(c => new CloudAdminChange(c.ChannelId, ids[c.Fingerprint], c.Delta, c.After, batch.Id))],
            CloudLedgerSource.QuarantineApply, userId, reason, [quarantine, batch]), ct);
        if (!committed)
            return Result.Fail("Um servidor pegou os canais deste jogador agora; tente de novo.");

        await CloudAudit.WriteAsync(governance, vaultId, userId, "quarantine.apply", reason, ct);
        return Result.Success();
    }
}

/// <summary>Conversão da definição de item vinda do mod para a entidade.</summary>
public static class CloudItemTypes
{
    public static CloudItemType FromDto(CloudItemDto d) => new()
    {
        Fingerprint = d.Fingerprint,
        ItemId = d.ItemId,
        ModId = d.ItemId[..d.ItemId.IndexOf(':')],
        DisplayName = d.DisplayName,
        Encoded = Convert.FromBase64String(d.Encoded)
    };
}
