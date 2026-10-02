using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Security;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Cloud;

/// <summary>
///     Gera a chave de um servidor de jogo e revoga as anteriores dele. A chave
///     em claro sai daqui UMA vez (para ser injetada no container) e nunca mais
///     pode ser lida: só o hash fica no banco.
///     Só o dono do servidor (ou o admin da instalação): quem tem a chave grava
///     na nuvem do dono.
/// </summary>
public sealed class IssueCloudServerKey(
    IServerRepository servers,
    ICloudCredentialRepository credentials,
    ICurrentUserScope scope,
    TimeProvider clock)
{
    public async Task<Result<string>> HandleAsync(Guid serverId, CancellationToken ct)
    {
        var auth = await scope.RequireAsync(serverId, ServerAccessPolicy.CanManageCloud, ct);
        if (!auth.Succeeded)
            return Result<string>.Fail(auth.Error!);

        var server = await servers.GetByIdAsync(serverId, ct);
        if (server is null)
            return Result<string>.Fail("Servidor não encontrado.");
        if (server.CloudVaultId is not { } vaultId)
            return Result<string>.Fail("Ligue o servidor a uma nuvem antes de gerar a chave.");

        var now = clock.GetUtcNow();
        foreach (var old in await credentials.ListActiveByServerAsync(serverId, ct))
        {
            old.Revoke(now);
            await credentials.UpdateAsync(old, ct);
        }

        var (key, prefix, hash) = CloudServerKey.Generate();
        await credentials.AddAsync(new CloudServerCredential
        {
            GameServerId = serverId,
            VaultId = vaultId,
            KeyPrefix = prefix,
            KeyHash = hash
        }, ct);

        return Result<string>.Success(key);
    }
}
