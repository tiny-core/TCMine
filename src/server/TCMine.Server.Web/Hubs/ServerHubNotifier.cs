using TCMine.Contracts.Hubs;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Web.Hubs;

/// <summary>Liga a porta da Application ao LauncherNotifier do SignalR.</summary>
public sealed class ServerHubNotifier(LauncherNotifier notifier, IResourceChanges changes) : IServerHubNotifier
{
    public Task NotifyModpackVersionPublishedAsync(Guid modpackId, Guid versionId, CancellationToken ct) =>
        notifier.ModpackVersionPublishedAsync(modpackId, versionId);

    public Task NotifyConsoleLineAsync(Guid serverId, ConsoleLineDto line, CancellationToken ct) =>
        notifier.ConsoleLineAsync(serverId, line);

    public Task NotifyPlayerCountChangedAsync(Guid serverId, int online, int max, CancellationToken ct)
    {
        // O evento de contagem só chega a quem acompanha o console (moderador
        // para cima). O "N online" da lista de servidores de qualquer jogador
        // vem da rebusca, e é este aviso que a pede. A contagem não passa por
        // repositório nenhum — vive em memória —, por isso o aviso sai daqui.
        changes.Touch(ResourceKind.Servers, serverId);

        return notifier.PlayerCountChangedAsync(serverId, online, max);
    }

    public Task NotifyRoleChangedAsync(Guid serverId, Guid userId, ServerRoleDto? role, CancellationToken ct) =>
        notifier.RoleChangedAsync(serverId, userId, role);
}
