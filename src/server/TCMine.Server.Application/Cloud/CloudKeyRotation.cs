using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Cloud;

/// <summary>
///     Revoga as chaves ativas de um servidor e, se pedido, emite uma nova. Num
///     lugar só porque toda troca de chave tem de revogar a anterior: duas chaves
///     válidas para o mesmo servidor seriam uma porta esquecida aberta.
/// </summary>
public static class CloudKeyRotation
{
    public static async Task RevokeAllAsync(ICloudCredentialRepository credentials, TimeProvider clock, Guid serverId,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        foreach (var key in await credentials.ListActiveByServerAsync(serverId, ct))
        {
            key.Revoke(now);
            await credentials.UpdateAsync(key, ct);
        }
    }

    /// <summary>Revoga as anteriores e devolve a chave nova em claro (só existe neste instante).</summary>
    public static async Task<string> RotateAsync(ICloudCredentialRepository credentials, TimeProvider clock,
        Guid serverId, Guid vaultId, CancellationToken ct)
    {
        await RevokeAllAsync(credentials, clock, serverId, ct);
        var (key, prefix, hash) = CloudServerKey.Generate();
        await credentials.AddAsync(new CloudServerCredential
        {
            GameServerId = serverId,
            VaultId = vaultId,
            KeyPrefix = prefix,
            KeyHash = hash
        }, ct);
        return key;
    }
}
