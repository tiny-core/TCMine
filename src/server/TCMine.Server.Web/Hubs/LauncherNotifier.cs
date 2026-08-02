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
public sealed class LauncherNotifier(IHubContext<MainHub, ILauncherClient> hub)
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
    ///     Avisa que o papel mudou.
    ///     Sem isto, quem foi rebaixado continua no grupo e recebendo o console
    ///     até reconectar. Quem chama deve também remover a conexão do grupo.
    /// </summary>
    public Task RoleChangedAsync(Guid serverId, ServerRoleDto role) =>
        hub.Clients.Group(MainHub.GroupFor(serverId)).RoleChanged(serverId, role);
}
