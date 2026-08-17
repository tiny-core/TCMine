using Microsoft.AspNetCore.SignalR;
using TCMine.Contracts.Hubs;
using TCMine.Contracts.Modpacks;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Security;
using TCMine.Server.Application.Servers;
using TCMine.Server.Web.Mapping;

namespace TCMine.Server.Web.Hubs;

/// <summary>
///     Canal persistente com os launchers conectados.
///     Hub &lt;ILauncherClient&gt; dá tipagem no envio: em vez de
///     Clients.All.SendAsync("ServerStatusChanged", ...) com string mágica,
///     escrevemos Clients.Group(...).ServerStatusChanged(...) e o compilador
///     avisa quando a assinatura muda de um lado só.
///     Regra que atravessa a classe: a autorização mora no CASO DE USO, não
///     aqui. A borda é plural — hub, endpoint HTTP, componente Blazor — e cada
///     borda nova esquece de novo; foi assim que o download de backup passou a
///     existir sem checar papel. O que sobra neste arquivo é a checagem de
///     assinatura de grupo, que não tem caso de uso porque não age sobre nada
///     além da própria conexão.
///     Em nenhuma hipótese a UI esconder um botão conta como proteção: quem tem
///     a URL do hub chama o método diretamente.
/// </summary>
public sealed class MainHub(
    ICurrentUserScope scope,
    IModpackRepository modpacks,
    ListAccessibleServers accessibleServers,
    SendServerCommand sendCommand,
    ConsoleBroadcaster broadcaster,
    IPlayerCountSource players) : Hub<ILauncherClient>, IServerHub
{
    public async Task<IReadOnlyList<ModpackDto>> GetModpacksAsync()
    {
        var packs = await modpacks.ListAsync(Context.ConnectionAborted);
        return [.. packs.Select(m => m.ToDto())];
    }

    public async Task<ModpackVersionDto> GetModpackVersionAsync(Guid versionId)
    {
        var version = await modpacks.GetVersionAsync(versionId, Context.ConnectionAborted);

        return version is null ? throw new HubException("Versão não encontrada.") : version.ToDto();
    }

    public async Task<IReadOnlyList<GameServerDto>> GetServersAsync()
    {
        // Filtrar aqui e não no cliente: a lista vazia é a resposta correta para
        // quem não foi convidado, e devolver tudo para a interface esconder
        // entregaria nome e endereço de servidores alheios a qualquer um que
        // olhasse a mensagem do hub.
        var servidores = await accessibleServers.HandleAsync(Context.ConnectionAborted);

        return [.. servidores.Select(s => s.ToDto(players))];
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

        // Só depois de entrar no grupo: começar a bombear antes abriria uma
        // janela em que as linhas saem para um grupo do qual esta conexão ainda
        // não faz parte — o primeiro pedaço do console se perderia.
        broadcaster.Subscribe(Context.ConnectionId, serverId);
    }

    public async Task UnsubscribeServerAsync(Guid serverId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(serverId));
        broadcaster.Unsubscribe(Context.ConnectionId, serverId);
    }

    /// <summary>
    ///     Queda de conexão é o caso comum, não a exceção — o jogador fecha o
    ///     launcher, o wi-fi cai, a máquina hiberna. Sem soltar aqui, o contador
    ///     de ouvintes nunca voltaria a zero e o console seguiria sendo lido do
    ///     Docker para ninguém, até o processo reiniciar.
    /// </summary>
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        broadcaster.Disconnect(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    public async Task<CommandResultDto> SendCommandAsync(
        Guid serverId,
        string command,
        IReadOnlyList<string> args)
    {
        // Sem checagem de papel aqui: ela mora no caso de uso, junto da
        // allowlist e da validação do comando. Repeti-la nesta borda criaria uma
        // segunda regra livre para divergir da primeira — e a borda é plural
        // (hub, endpoint, componente), então cada nova esqueceria de novo.
        var result = await sendCommand.HandleAsync(
            serverId, command, args, Context.ConnectionAborted);

        // Recusa vira resposta, não exceção: um comando negado não deve derrubar
        // a conexão do launcher.
        return result.Succeeded
            ? new CommandResultDto(true, result.Value, null)
            : new CommandResultDto(false, null, result.Error);
    }

    /// <summary>
    ///     Nome do grupo de um servidor. Método em vez de interpolação solta
    ///     para não haver divergência entre o join e o envio — um typo ali
    ///     significaria eventos indo para o vazio, sem erro nenhum.
    /// </summary>
    public static string GroupFor(Guid serverId) => $"server:{serverId}";

    private async Task<ServerRoleDto> RequireRoleAsync(Guid serverId)
    {
        var role = await scope.GetRoleAsync(serverId, Context.ConnectionAborted);

        // Mesma mensagem para "não existe" e "não tenho acesso". Diferenciar
        // permitiria descobrir quais servidores existem só variando o id.
        return role ?? throw new HubException("Servidor não encontrado.");
    }
}
