using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Cloud;

/// <summary>
///     Resolve a chave de um servidor de jogo para "qual servidor, qual nuvem".
///     É a porta de entrada de toda a API do mod: a nuvem da requisição vem
///     daqui, nunca do corpo — é isso que garante que um servidor só toque a
///     nuvem do próprio dono.
///     Recusa também a chave de um servidor que foi desligado da nuvem depois de
///     recebê-la: desligar no painel tem de valer na próxima requisição.
/// </summary>
public sealed class AuthenticateCloudServer(ICloudCredentialRepository credentials, IServerRepository servers)
{
    public async Task<Result<CloudServerContext>> HandleAsync(string? key, CancellationToken ct)
    {
        var prefix = CloudServerKey.PrefixOf(key);
        if (prefix is null)
            return Result<CloudServerContext>.Fail("Chave ausente ou mal formada.");

        var credential = await credentials.FindByPrefixAsync(prefix, ct);
        if (credential is null || !credential.IsActive || !CloudServerKey.Matches(key!, credential.KeyHash))
            return Result<CloudServerContext>.Fail("Chave inválida.");

        var server = await servers.GetByIdAsync(credential.GameServerId, ct);
        if (server is null || server.CloudVaultId != credential.VaultId)
            return Result<CloudServerContext>.Fail("Este servidor não está ligado a esta nuvem.");

        return Result<CloudServerContext>.Success(new CloudServerContext(server.Id, credential.VaultId, credential.Id));
    }
}
