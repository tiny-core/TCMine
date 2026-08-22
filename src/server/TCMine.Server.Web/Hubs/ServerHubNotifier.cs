using TCMine.Contracts.Hubs;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Web.Hubs;

/// <summary>Liga a porta da Application ao LauncherNotifier do SignalR.</summary>
public sealed class ServerHubNotifier(LauncherNotifier notifier) : IServerHubNotifier
{
    public Task NotifyModpackVersionPublishedAsync(Guid modpackId, Guid versionId, CancellationToken ct) =>
        notifier.ModpackVersionPublishedAsync(modpackId, versionId);

    public Task NotifyConsoleLineAsync(Guid serverId, ConsoleLineDto line, CancellationToken ct) =>
        notifier.ConsoleLineAsync(serverId, line);

    public Task NotifyPlayerCountChangedAsync(Guid serverId, int online, int max, CancellationToken ct) =>
        notifier.PlayerCountChangedAsync(serverId, online, max);

    public Task NotifyRoleChangedAsync(Guid serverId, Guid userId, ServerRoleDto? role, CancellationToken ct) =>
        notifier.RoleChangedAsync(serverId, userId, role);
}
