using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Cloud;

/// <summary>
///     Renova os leases que o servidor de jogo diz segurar. Os que não renovam
///     (outro servidor tomou, época antiga) voltam em <c>Lost</c>: o mod trava
///     aqueles canais, porque continuar mexendo neles só geraria quarentena.
///     Uma leitura e um UPDATE para o heartbeat inteiro, e não um par por
///     jogador (fase 5.5): a decisão de quem perdeu é tomada sobre a leitura, e
///     o UPDATE só toca o que ainda é deste servidor — se outro tomou um lease
///     no meio do caminho, ele não casa e é renovado por ninguém, e o próximo
///     heartbeat o devolve como perdido.
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

        var asked = request.Leases
            .Select(held => (Held: held, Player: CloudChannel.NormalizePlayerUuid(held.PlayerUuid)))
            .ToList();
        var players = asked.Where(a => a.Player is not null).Select(a => a.Player!).Distinct().ToList();
        var leases = (await storage.GetLeasesAsync(ctx.VaultId, players, ct)).ToDictionary(l => l.PlayerUuid);

        var lost = new List<string>();
        var renew = new List<string>();
        foreach (var (held, player) in asked)
        {
            if (player is not null && leases.TryGetValue(player, out var lease)
                                   && lease.CanRenew(ctx.ServerId, held.Epoch))
                renew.Add(player);
            else
                lost.Add(held.PlayerUuid);
        }

        await storage.RenewLeasesAsync(ctx.VaultId, ctx.ServerId, renew, clock.GetUtcNow(), vault.LeaseTtl, ct);

        return CloudCallResult<CloudHeartbeatReply>.Ok(new CloudHeartbeatReply(lost, vault.PolicyVersion));
    }
}
