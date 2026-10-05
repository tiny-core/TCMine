using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Cloud;

/// <summary>
///     Registro das decisões do painel da nuvem (quem fez o quê). Num lugar só
///     para toda ação usar o mesmo formato e o mesmo teto de tamanho.
/// </summary>
public static class CloudAudit
{
    public static Task WriteAsync(ICloudGovernanceRepository governance, Guid vaultId, Guid? userId, string action,
        string details, CancellationToken ct) =>
        governance.AddAuditAsync(new CloudAdminAuditEntry
        {
            VaultId = vaultId,
            ActorUserId = userId,
            Action = action,
            Details = details.Length > 2048 ? details[..2045] + "..." : details
        }, ct);
}

/// <summary>
///     Carrega os leases de jogadores para uma decisão do painel que mexe em
///     saldos, exigindo que estejam livres. Um servidor segurando o lease tem os
///     saldos em memória e não veria a mudança — o dono espera o jogador sair (ou
///     libera o lease à força).
/// </summary>
public static class CloudLeaseGuard
{
    /// <param name="releasableBy">servidor cujo lease pode ser liberado junto (o do incidente de rollback)</param>
    public static async Task<(List<(CloudLease, long?)>? Leases, string? Error)> LoadFreeAsync(
        ICloudStorageRepository storage, Guid vaultId, IEnumerable<string> players, DateTimeOffset now,
        CancellationToken ct, Guid? releasableBy = null)
    {
        var leases = new List<(CloudLease, long?)>();
        foreach (var player in players.Distinct())
        {
            var lease = await storage.GetLeaseAsync(vaultId, player, ct);
            if (lease is null)
            {
                lease = new CloudLease { VaultId = vaultId, PlayerUuid = player };
                lease.MarkAdminChange();
                leases.Add((lease, null));
                continue;
            }

            var read = lease.Version;
            if (!lease.IsFree(now))
            {
                if (releasableBy is null || lease.HolderServerId != releasableBy)
                    return (null, $"O jogador {player} está com os canais num servidor agora. Espere ele sair ou libere o lease.");
                lease.ForceRelease();
            }
            else
            {
                lease.MarkAdminChange();
            }
            leases.Add((lease, read));
        }
        return (leases, null);
    }
}
