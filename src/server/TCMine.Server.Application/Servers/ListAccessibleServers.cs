using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Security;

namespace TCMine.Server.Application.Servers;

/// <summary>
///     Os servidores que o usuário atual enxerga: os que tem vínculo, mais
///     qualquer um sem whitelist — um servidor público não tem porque esconder
///     a própria existência de quem ainda não foi convidado, só o controle
///     sobre ele.
///     Não devolve <c>Result</c> porque não há falha de regra possível: quem não
///     tem vínculo nenhum e só vê públicos vê uma lista correta, não um erro.
///     Recusar seria pior — diria ao jogador que existe algo que ele não pode
///     ver.
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
        var papelPorServidor = vinculos.ToDictionary(m => m.GameServerId, m => m.Role.ToDto());

        // Uma consulta e um filtro em memória, em vez de N buscas por id: a
        // lista de servidores de uma instalação é pequena, e o custo de trazê-la
        // inteira é menor que o de uma ida ao banco por vínculo.
        var todosServidores = await servers.ListAllAsync(ct);

        return
        [
            .. todosServidores
                // Com vínculo: enxerga pelo papel que tem. Sem vínculo: só
                // enxerga o que é público (sem whitelist) — e nesse caso o
                // papel é Member, o mesmo "vê status, sem console" de quem foi
                // convidado só para jogar.
                .Where(s => papelPorServidor.ContainsKey(s.Id) || !s.WhitelistEnabled)
                .Select(s => new AccessibleServer(
                    s,
                    papelPorServidor.TryGetValue(s.Id, out var papel) ? papel : ServerRoleDto.Member))
        ];
    }
}
