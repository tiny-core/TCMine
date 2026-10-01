using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Security;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Servers;

/// <summary>
///     Os servidores que o usuário atual enxerga — TODOS eles, mesmo os que ele
///     ainda não pode entrar. Esconder a existência de um servidor com
///     whitelist não protegeria nada a mais do que já esconder o endereço de
///     conexão, e impediria o próprio motivo de existir do pedido de acesso:
///     sem ver o servidor, não há o que pedir.
///     Não devolve <c>Result</c> porque não há falha de regra possível: quem não
///     tem vínculo nenhum ainda vê uma lista correta, não um erro. Recusar
///     seria pior — diria ao jogador que existe algo que ele não pode ver.
/// </summary>
public sealed class ListAccessibleServers(
    IServerRepository servers,
    IMembershipRepository memberships,
    IAccessRequestRepository requests,
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
            return [.. todos.Select(s => new AccessibleServer(s, ServerRoleDto.Owner, ServerAccessState.Granted))];
        }

        var vinculos = await memberships.ListByUserAsync(userId, ct);
        var papelPorServidor = vinculos.ToDictionary(m => m.GameServerId, m => m.Role.ToDto());

        var pendentes = await requests.ListPendingByUserAsync(userId, ct);
        var pedidoPendentePara = pendentes.Select(r => r.GameServerId).ToHashSet();

        // Uma consulta e um filtro em memória, em vez de N buscas por id: a
        // lista de servidores de uma instalação é pequena, e o custo de trazê-la
        // inteira é menor que o de uma ida ao banco por servidor.
        var todosServidores = await servers.ListAllAsync(ct);

        return [.. todosServidores.Select(s => Resolve(s, papelPorServidor, pedidoPendentePara))];
    }

    private static AccessibleServer Resolve(
        GameServer server,
        Dictionary<Guid, ServerRoleDto> papelPorServidor,
        HashSet<Guid> pedidoPendentePara)
    {
        if (papelPorServidor.TryGetValue(server.Id, out var papel))
            return new AccessibleServer(server, papel, ServerAccessState.Granted);

        // Sem whitelist: liberado para qualquer autenticado, sem precisar de
        // pedido — é o que faz dele um servidor público.
        if (!server.WhitelistEnabled)
            return new AccessibleServer(server, ServerRoleDto.Member, ServerAccessState.Granted);

        var estado = pedidoPendentePara.Contains(server.Id) ? ServerAccessState.Pending : ServerAccessState.None;

        // O papel aqui não importa — sem Granted não há console nem comando —
        // mas o campo é obrigatório no DTO, e Member é o valor que combina com
        // "ainda não tem nada".
        return new AccessibleServer(server, ServerRoleDto.Member, estado);
    }
}
