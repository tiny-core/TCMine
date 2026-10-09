using System.Text.Json;
using Microsoft.Extensions.Logging;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Cloud;

/// <summary>
///     Boot do servidor de jogo: registra o contato (versão do mod, mundo),
///     confere se o mundo voltou no tempo e devolve a configuração e a política
///     da nuvem. Sem hello concluído o mod não pede canal nenhum.
///     Mundo que voltou no tempo (plano §5.2): o checkpoint declarado está ATRÁS
///     de lotes que este servidor já aplicou. Só é conferido quando o mundo é o
///     mesmo da conexão anterior — mundo diferente é mapa novo, e aí o checkpoint
///     vazio é o normal. Com incidente aberto, a nuvem fica somente leitura
///     neste servidor até o dono decidir no painel.
/// </summary>
public sealed partial class CloudHello(
    ICloudCredentialRepository credentials,
    ICloudGovernanceRepository governance,
    TimeProvider clock,
    ILogger<CloudHello> logger)
{
    public async Task<CloudCallResult<CloudHelloReply>> HandleAsync(CloudServerContext ctx, CloudHelloRequest request,
        CancellationToken ct)
    {
        var vault = await credentials.GetVaultAsync(ctx.VaultId, ct);
        if (vault is null)
            return CloudCallResult<CloudHelloReply>.Forbidden("Nuvem não encontrada.");

        var incident = await governance.GetOpenIncidentAsync(ctx.ServerId, ct)
                       ?? await DetectRollbackAsync(ctx, request.Checkpoint, ct);

        var credential = await credentials.FindByIdAsync(ctx.CredentialId, ct);
        if (credential is not null)
        {
            credential.RecordContact(clock.GetUtcNow(), request.ModVersion, request.Checkpoint.WorldId);
            await credentials.UpdateAsync(credential, ct);
        }

        LogHello(ctx.ServerId, request.ModVersion ?? "?", request.Checkpoint.WorldId,
            request.Checkpoint.Players?.Count ?? 0);

        var policy = await CloudPolicy.LoadAsync(governance, vault, ct);
        var readOnlyReason = !vault.IsEnabled ? "Nuvem desligada pelo dono."
            : incident is not null ? "Mundo voltou no tempo: aguardando o dono decidir no painel."
            : null;

        return CloudCallResult<CloudHelloReply>.Ok(new CloudHelloReply(
            CloudProtocol.Current,
            policy.PolicyMode,
            policy.PolicyVersion,
            policy.Rules,
            new CloudQuotaDto(vault.MaxTypesPerChannel, vault.MaxTotalPerChannel),
            vault.MaxItemBytes,
            vault.MaxChannelsPerPlayer,
            vault.LeaseTtlMinutes * 60,
            readOnlyReason is not null,
            readOnlyReason));
    }

    private async Task<CloudRollbackIncident?> DetectRollbackAsync(CloudServerContext ctx, CloudCheckpoint checkpoint,
        CancellationToken ct)
    {
        var previousWorld = await governance.PreviousWorldAsync(ctx.ServerId, ctx.CredentialId, ct);
        if (previousWorld != checkpoint.WorldId)
            return null; // primeiro contato ou mapa novo: nada a comparar

        var declared = (checkpoint.Players ?? new Dictionary<string, CloudSeqPosition>())
            .Select(kv => (Player: CloudChannel.NormalizePlayerUuid(kv.Key), kv.Value))
            .Where(x => x.Player is not null)
            .ToDictionary(x => x.Player!, x => (x.Value.Epoch, x.Value.Seq));

        var behind = new List<string>();
        foreach (var (player, applied) in await governance.LastAppliedByServerAsync(ctx.VaultId, ctx.ServerId, ct))
        {
            var mine = declared.GetValueOrDefault(player);
            if (mine.CompareTo(applied) < 0)
                behind.Add($"{player}: mundo em {mine.Epoch}/{mine.Item2}, TCMine em {applied.Epoch}/{applied.Seq}");
        }

        if (behind.Count == 0)
            return null;

        var detail = string.Join("; ", behind);
        var incident = new CloudRollbackIncident
        {
            VaultId = ctx.VaultId,
            ServerId = ctx.ServerId,
            WorldId = checkpoint.WorldId,
            CheckpointJson = JsonSerializer.Serialize(checkpoint),
            Detail = detail.Length > 2048 ? detail[..2045] + "..." : detail
        };
        await governance.AddIncidentAsync(incident, ct);
        LogRollback(ctx.ServerId, behind.Count);
        return incident;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message =
            "Nuvem: hello do servidor {ServerId} (mod {ModVersion}, mundo {WorldId}, {Players} jogadores no checkpoint).")]
    private partial void LogHello(Guid serverId, string modVersion, Guid worldId, int players);

    [LoggerMessage(Level = LogLevel.Warning,
        Message =
            "Nuvem: o mundo do servidor {ServerId} voltou no tempo ({Players} jogadores atrás do TCMine); nuvem em somente leitura até o dono decidir.")]
    private partial void LogRollback(Guid serverId, int players);
}
