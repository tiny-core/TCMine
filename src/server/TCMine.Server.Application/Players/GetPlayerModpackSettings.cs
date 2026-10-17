using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Players;

/// <summary>
///     As opções que o jogador gravou neste modpack. Só as DELE: a conta vem da
///     sessão, nunca de um parâmetro — senão bastaria trocar um id para ler as
///     teclas de outra pessoa.
/// </summary>
public sealed class GetPlayerModpackSettings(IPlayerSettingsRepository settings, ICurrentUserScope scope)
{
    /// <returns>Sucesso com nulo quando ele nunca gravou nada neste pack.</returns>
    public async Task<Result<PlayerModpackSettings?>> HandleAsync(Guid modpackId, CancellationToken ct)
    {
        if (scope.UserId is not { } userId)
            return Result<PlayerModpackSettings?>.Fail("Entre na sua conta para ler as suas opções.");

        return Result<PlayerModpackSettings?>.Success(await settings.GetAsync(userId, modpackId, ct));
    }
}
