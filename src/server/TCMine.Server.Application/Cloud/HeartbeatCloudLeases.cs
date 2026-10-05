using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Cloud;

/// <summary>
///     Renova os leases que o servidor de jogo diz segurar. Os que não renovam
///     (outro servidor tomou, época antiga) voltam em <c>Lost</c>: o mod trava
///     aqueles canais, porque continuar mexendo neles só geraria quarentena.
/// </summary>
public sealed class HeartbeatCloudLeases(
    ICloudStorageRepository storage,
    ICloudCredentialRepository credentials,
    TimeProvider clock)
{
    public async Task<CloudCallResult<CloudHeartbeatReply>> HandleAsync(CloudServerContext ctx,
        CloudHeartbeatRequest request, CancellationToken ct)
    {
        if (request.Leases.Count > CloudProtocol.MaxLeasesPerHeartbeat)
            return CloudCallResult<CloudHeartbeatReply>.Invalid("Leases demais num heartbeat.");

        var vault = await credentials.GetVaultAsync(ctx.VaultId, ct);
        if (vault is null)
            return CloudCallResult<CloudHeartbeatReply>.Forbidden("Nuvem não encontrada.");

        var now = clock.GetUtcNow();
        var lost = new List<string>();
        foreach (var held in request.Leases)
        {
            var player = CloudChannel.NormalizePlayerUuid(held.PlayerUuid);
            var lease = player is null ? null : await storage.GetLeaseAsync(ctx.VaultId, player, ct);
            if (lease is null)
            {
                lost.Add(held.PlayerUuid);
                continue;
            }

            var read = lease.Version;
            // Conflito aqui não é perda: alguém (um lote) mexeu no lease agora,
            // e o próximo heartbeat renova. Só "não é mais seu" vira Lost.
            if (!lease.Renew(ctx.ServerId, held.Epoch, now, vault.LeaseTtl))
                lost.Add(held.PlayerUuid);
            else
                await storage.SaveLeaseAsync(lease, read, ct);
        }

        return CloudCallResult<CloudHeartbeatReply>.Ok(new CloudHeartbeatReply(lost, vault.PolicyVersion));
    }
}
