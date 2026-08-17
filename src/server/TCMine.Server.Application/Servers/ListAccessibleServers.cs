using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Security;

namespace TCMine.Server.Application.Servers;

/// <summary>
///     Os servidores que o usuário atual enxerga.
///     Não devolve <c>Result</c> porque não há falha de regra possível: quem não
///     tem vínculo nenhum vê uma lista vazia, e isso é uma resposta correta, não
///     um erro. Recusar seria pior — diria ao jogador que existe algo que ele
///     não pode ver.
/// </summary>
public sealed class ListAccessibleServers(
    IServerRepository servers,
    IMembershipRepository memberships,
    ICurrentUserScope scope)
{
    public async Task<IReadOnlyList<AccessibleServer>> HandleAsync(CancellationToken ct)
    {
        if (scope.UserId is not { } userId)
            return [];

        // Admin da instalação enxerga tudo, como Owner — é a mesma regra que o
        // ICurrentUserScope aplica ao responder o papel, e repeti-la aqui evita
        // que o painel dele apareça vazio por não haver Membership gravado.
        if (scope.IsInstanceAdmin)
        {
            var todos = await servers.ListAllAsync(ct);
            return [.. todos.Select(s => new AccessibleServer(s, ServerRoleDto.Owner))];
        }

        var vinculos = await memberships.ListByUserAsync(userId, ct);
        if (vinculos.Count == 0)
            return [];

        var papelPorServidor = vinculos.ToDictionary(m => m.GameServerId, m => m.Role.ToDto());

        // Uma consulta e um filtro em memória, em vez de N buscas por id: a
        // lista de servidores de uma instalação é pequena, e o custo de trazê-la
        // inteira é menor que o de uma ida ao banco por vínculo.
        var todosServidores = await servers.ListAllAsync(ct);

        return
        [
            .. todosServidores
                .Where(s => papelPorServidor.ContainsKey(s.Id))
                .Select(s => new AccessibleServer(s, papelPorServidor[s.Id]))
        ];
    }
}
