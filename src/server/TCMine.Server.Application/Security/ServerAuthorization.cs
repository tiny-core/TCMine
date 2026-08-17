using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Security;

/// <summary>
///     A checagem de papel que todo caso de uso de servidor faz na primeira
///     linha.
///     Um helper e não oito blocos copiados: a mensagem devolvida faz parte da
///     garantia (ver abaixo), e uma cópia que a escrevesse diferente abriria
///     justamente o vazamento que as outras sete fecham.
/// </summary>
public static class ServerAuthorization
{
    /// <summary>
    ///     Autoriza antes de qualquer trabalho. Chamar no topo do
    ///     <c>HandleAsync</c> tem dois efeitos: nada é carregado para quem não
    ///     pode agir, e a resposta é a mesma para servidor inexistente e para
    ///     servidor alheio — <see cref="ICurrentUserScope.GetRoleAsync" /> já
    ///     devolve nulo nos dois casos. Distinguir os dois permitiria mapear
    ///     quais servidores existem só variando o id.
    /// </summary>
    public static async Task<Result> RequireAsync(
        this ICurrentUserScope scope,
        Guid gameServerId,
        Func<ServerRoleDto, bool> permite,
        CancellationToken ct)
    {
        var role = await scope.GetRoleAsync(gameServerId, ct);

        return role is { } papel && permite(papel)
            ? Result.Success()
            : Result.Fail("Servidor não encontrado.");
    }
}
