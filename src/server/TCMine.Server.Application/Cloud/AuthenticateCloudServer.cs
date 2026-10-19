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
public sealed class AuthenticateCloudServer(ICloudCredentialRepository credentials)
{
    public async Task<Result<CloudServerContext>> HandleAsync(string? key, CancellationToken ct)
    {
        var prefix = CloudServerKey.PrefixOf(key);
        if (prefix is null)
            return Result<CloudServerContext>.Fail("Chave ausente ou mal formada.");

        var found = await credentials.FindKeyAsync(prefix, ct);
        if (found is null || !found.IsActive || !CloudServerKey.Matches(key!, found.KeyHash))
            return Result<CloudServerContext>.Fail("Chave inválida.");

        if (found.ServerVaultId != found.VaultId)
            return Result<CloudServerContext>.Fail("Este servidor não está ligado a esta nuvem.");

        return Result<CloudServerContext>.Success(
            new CloudServerContext(found.GameServerId, found.VaultId, found.CredentialId));
    }
}
