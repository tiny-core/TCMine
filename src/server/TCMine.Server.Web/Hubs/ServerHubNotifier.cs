using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Web.Hubs;

/// <summary>Liga a porta da Application ao LauncherNotifier do SignalR.</summary>
public sealed class ServerHubNotifier(LauncherNotifier notifier) : IServerHubNotifier
{
    public Task NotifyModpackVersionPublishedAsync(Guid modpackId, Guid versionId, CancellationToken ct) =>
        notifier.ModpackVersionPublishedAsync(modpackId, versionId);
}
