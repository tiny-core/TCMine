using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Cloud;

/// <summary>
///     Logout concluído no servidor de jogo: libera os canais para outro
///     servidor. Só libera se todos os lotes da época chegaram
///     (<see cref="CloudLease.Release" />); senão continua preso e expira pelo
///     TTL, o que é o lado seguro.
/// </summary>
public sealed class ReleaseCloudLease(ICloudStorageRepository storage)
{
    public async Task<CloudCallResult<CloudReleaseReply>> HandleAsync(CloudServerContext ctx,
        CloudReleaseRequest request, CancellationToken ct)
    {
        var player = CloudChannel.NormalizePlayerUuid(request.PlayerUuid);
        if (player is null)
            return CloudCallResult<CloudReleaseReply>.Invalid("UUID do jogador inválido.");

        var lease = await storage.GetLeaseAsync(ctx.VaultId, player, ct);
        if (lease is null)
            return CloudCallResult<CloudReleaseReply>.Ok(new CloudReleaseReply(false));

        var read = lease.Version;
        if (!lease.Release(ctx.ServerId, request.Epoch, request.LastSeq))
            return CloudCallResult<CloudReleaseReply>.Ok(new CloudReleaseReply(false));

        return await storage.SaveLeaseAsync(lease, read, ct)
            ? CloudCallResult<CloudReleaseReply>.Ok(new CloudReleaseReply(true))
            : CloudCallResult<CloudReleaseReply>.Conflict("O lease mudou agora; tente de novo.");
    }
}
