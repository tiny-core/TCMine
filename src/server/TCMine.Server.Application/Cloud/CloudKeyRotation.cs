using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Cloud;

/// <summary>
///     Revoga a chave de um servidor e, se pedido, emite uma nova. Num lugar só
///     porque toda troca de chave tem de matar a anterior: duas chaves válidas
///     para o mesmo servidor seriam uma porta esquecida aberta.
///     Há uma linha por servidor, e a chave nova SOBRESCREVE a velha nela — é a
///     mesma gravação que revoga, então não existe instante com as duas.
/// </summary>
public static class CloudKeyRotation
{
    public static Task RevokeAsync(ICloudCredentialRepository credentials, TimeProvider clock, Guid serverId,
        CancellationToken ct) =>
        credentials.RevokeAsync(serverId, clock.GetUtcNow(), ct);

    /// <summary>Emite a chave nova no lugar da anterior e a devolve em claro (só existe neste instante).</summary>
    public static async Task<string> RotateAsync(ICloudCredentialRepository credentials, TimeProvider clock,
        Guid serverId, Guid vaultId, CancellationToken ct)
    {
        var (key, prefix, hash) = CloudServerKey.Generate();

        if (await credentials.FindByServerAsync(serverId, ct) is { } current)
        {
            current.Reissue(vaultId, prefix, hash, clock.GetUtcNow());
            await credentials.UpdateAsync(current, ct);
        }
        else
            await credentials.AddAsync(CloudServerCredential.Issue(serverId, vaultId, prefix, hash), ct);

        return key;
    }
}
