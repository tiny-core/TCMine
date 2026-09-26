using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Security;

/// <summary>
///     A checagem de papel que todo caso de uso de modpack faz na primeira
///     linha. Ao lado da <see cref="ServerAuthorization" /> e pela mesma razão.
/// </summary>
public static class ModpackAuthorization
{
    /// <summary>
    ///     Autoriza antes de qualquer trabalho. A mensagem é a mesma para modpack
    ///     inexistente e para modpack alheio — <see cref="ICurrentUserScope.GetModpackRoleAsync" />
    ///     já devolve nulo nos dois casos, e distinguir os dois permitiria mapear
    ///     quais modpacks existem só variando o id.
    /// </summary>
    public static async Task<Result> RequireAsync(
        this ICurrentUserScope scope,
        Guid modpackId,
        Func<ModpackRoleDto, bool> permite,
        CancellationToken ct)
    {
        var role = await scope.GetModpackRoleAsync(modpackId, ct);

        return role is { } papel && permite(papel)
            ? Result.Success()
            : Result.Fail("Modpack não encontrado.");
    }
}
