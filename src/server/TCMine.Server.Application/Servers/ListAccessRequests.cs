using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Servers;

/// <summary>
///     Os pedidos de acesso que o usuário atual pode decidir.
///     Como cada servidor é de um dono, isto filtra sozinho: um admin só vê
///     pedidos dos servidores onde ele é Admin/Owner (ou de todos, se for
///     admin da instalação) — a mesma regra de <see cref="ListAccessibleServers" />,
///     sem precisar de nenhuma tabela nova de dono.
/// </summary>
public sealed class ListAccessRequests(
    IServerRepository servers,
    IMembershipRepository memberships,
    IAccessRequestRepository requests,
    ICurrentUserScope scope)
{
    public async Task<IReadOnlyList<AccessRequestView>> HandleAsync(CancellationToken ct)
    {
        if (scope.UserId is not { } userId)
            return [];

        if (scope.IsInstanceAdmin)
        {
            var todos = await servers.ListAllAsync(ct);
            return await requests.ListPendingForServersAsync([.. todos.Select(s => s.Id)], ct);
        }

        var vinculos = await memberships.ListByUserAsync(userId, ct);

        var administrados = vinculos
            .Where(m => m.Role >= ServerRole.Admin)
            .Select(m => m.GameServerId)
            .ToArray();

        return administrados.Length == 0
            ? []
            : await requests.ListPendingForServersAsync(administrados, ct);
    }
}
