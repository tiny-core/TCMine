using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Security;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Servers;

/// <summary>
///     Quem tem acesso a um servidor e quais convites estão em aberto.
///     Leitura, mas autorizada como as escritas: a lista de membros diz quem
///     joga onde, e os convites pendentes revelam quem foi chamado e com que
///     papel. Nenhum dos dois é informação para qualquer membro.
/// </summary>
public sealed class ListServerAccess(
    IMembershipRepository memberships,
    IInviteRepository invites,
    ICurrentUserScope scope)
{
    public async Task<Result<ServerAccessView>> HandleAsync(Guid gameServerId, CancellationToken ct)
    {
        var auth = await scope.RequireAsync(gameServerId, ServerAccessPolicy.CanManageMembers, ct);
        if (!auth.Succeeded)
            return Result<ServerAccessView>.Fail(auth.Error!);

        var membros = await memberships.ListWithUsersAsync(gameServerId, ct);
        var todos = await invites.ListByServerAsync(gameServerId, ct);

        var agora = DateTimeOffset.UtcNow;

        // Só os que ainda servem: um histórico de convites usados e vencidos
        // encheria a tela com linhas sobre as quais não há o que fazer.
        var pendentes = todos.Where(i => i.IsUsable(agora)).ToList();

        return Result<ServerAccessView>.Success(new ServerAccessView(membros, pendentes));
    }
}
