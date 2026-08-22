using Microsoft.AspNetCore.SignalR;
using TCMine.Contracts.Hubs;
using TCMine.Contracts.Servers;

namespace TCMine.Server.Web.Hubs;

/// <summary>
///     Envia eventos aos launchers a partir de fora do Hub.
///     Existe porque quem publica um modpack ou observa um container não tem
///     acesso à instância do Hub — ela só vive durante uma chamada de cliente.
///     O IHubContext é o caminho oficial, e encapsulá-lo aqui evita espalhar
///     nomes de grupo pelo código.
/// </summary>
public sealed class LauncherNotifier(
    IHubContext<MainHub, ILauncherClient> hub,
    ConsoleBroadcaster broadcaster)
{
    public Task ModpackVersionPublishedAsync(Guid modpackId, Guid versionId) =>
        hub.Clients.All.ModpackVersionPublished(modpackId, versionId);

    public Task ServerStatusChangedAsync(Guid serverId, GameServerStatus status) =>
        hub.Clients.Group(MainHub.GroupFor(serverId)).ServerStatusChanged(serverId, status);

    public Task PlayerCountChangedAsync(Guid serverId, int online, int max) => hub.Clients
        .Group(MainHub.GroupFor(serverId)).ServerPlayerCountChanged(serverId, online, max);

    public Task ConsoleLineAsync(Guid serverId, ConsoleLineDto line) =>
        hub.Clients.Group(MainHub.GroupFor(serverId)).ConsoleLine(serverId, line);

    /// <summary>
    ///     Avisa UM usuário que o papel dele neste servidor mudou, e tira as
    ///     conexões dele do grupo.
    ///     As duas coisas juntas de propósito: avisar sem expulsar deixaria o
    ///     console correndo para quem acabou de perder o direito de lê-lo, e
    ///     confiar no launcher para se desinscrever seria confiar no cliente
    ///     para aplicar a própria punição.
    ///     Vai para Clients.User e não para o grupo — o grupo é todo mundo que
    ///     acompanha o servidor, e o papel dos outros não mudou.
    /// </summary>
    public async Task RoleChangedAsync(Guid serverId, Guid userId, ServerRoleDto? role)
    {
        await hub.Clients.User(userId.ToString()).RoleChanged(serverId, role);

        // Moderator é o piso para ler console (ConsoleCommandPolicy). Quem
        // continua acima dele segue acompanhando; quem caiu, sai.
        if (role is >= ServerRoleDto.Moderator)
            return;

        foreach (var connectionId in broadcaster.ConnectionsOf(userId, serverId))
        {
            await hub.Groups.RemoveFromGroupAsync(connectionId, MainHub.GroupFor(serverId));
            broadcaster.Unsubscribe(connectionId, serverId);
        }
    }
}
