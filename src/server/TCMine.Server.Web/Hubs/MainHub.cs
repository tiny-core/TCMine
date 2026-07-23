using Microsoft.AspNetCore.SignalR;
using TCMine.Contracts.Hubs;
using TCMine.Contracts.Modpacks;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Security;

namespace TCMine.Server.Web.Hubs;

/// <summary>
///     Canal persistente com os launchers conectados.
///     Hub &lt;ILauncherClient&gt; dá tipagem no envio: em vez de
///     Clients.All.SendAsync("ServerStatusChanged", ...) com string mágica,
///     escrevemos Clients.Group(...).ServerStatusChanged(...) e o compilador
///     avisa quando a assinatura muda de um lado só.
///     Regra que atravessa a classe: toda autorização acontece aqui. A UI
///     esconder um botão é conveniência, não proteção — quem tem a URL do hub
///     chama o método diretamente.
/// </summary>
public sealed class MainHub(ICurrentUserScope scope) : Hub<ILauncherClient>, IServerHub
{
    public Task<IReadOnlyList<ModpackDto>> GetModpacksAsync()
    {
        // TODO: consultar via Application quando o caso de uso existir.
        return Task.FromResult<IReadOnlyList<ModpackDto>>([]);
    }

    public Task<ModpackVersionDto> GetModpackVersionAsync(Guid versionId)
    {
        throw new HubException("Ainda não implementado.");
    }

    public Task<IReadOnlyList<GameServerDto>> GetServersAsync()
    {
        return Task.FromResult<IReadOnlyList<GameServerDto>>([]);
    }

    public async Task SubscribeServerAsync(Guid serverId)
    {
        var role = await RequireRoleAsync(serverId);

        // Entrar no grupo é o que habilita receber status e console. Sem a
        // checagem, qualquer usuário autenticado assinaria qualquer
        // servidor e leria IP de jogador e chat alheio.
        if (!ConsoleCommandPolicy.CanReadConsole(role))
            throw new HubException("Sem permissão para acompanhar este servidor.");

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(serverId));
    }

    public Task UnsubscribeServerAsync(Guid serverId)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(serverId));
    }

    public async Task<CommandResultDto> SendCommandAsync(
        Guid serverId,
        string command,
        IReadOnlyList<string> args)
    {
        var role = await RequireRoleAsync(serverId);

        if (!ConsoleCommandPolicy.IsAllowed(role, command))
            // Não vaza qual seria o papel necessário: informação de
            // autorização é pista para quem está sondando o sistema.
            return new CommandResultDto(
                false,
                null,
                "Comando não permitido para o seu nível de acesso.");

        // TODO: traduzir para RCON via IRconClient. A senha do RCON nunca
        // sai do servidor — é aqui que a tradução acontece.
        return new CommandResultDto(false, null, "Ainda não implementado.");
    }

    /// <summary>
    ///     Nome do grupo de um servidor. Método em vez de interpolação solta
    ///     para não haver divergência entre o join e o envio — um typo ali
    ///     significaria eventos indo para o vazio, sem erro nenhum.
    /// </summary>
    public static string GroupFor(Guid serverId)
    {
        return $"server:{serverId}";
    }

    private async Task<ServerRoleDto> RequireRoleAsync(Guid serverId)
    {
        var role = await scope.GetRoleAsync(serverId, Context.ConnectionAborted);

        // Mesma mensagem para "não existe" e "não tenho acesso". Diferenciar
        // permitiria descobrir quais servidores existem só variando o id.
        return role ?? throw new HubException("Servidor não encontrado.");
    }
}