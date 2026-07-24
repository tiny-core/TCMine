namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Envia eventos aos launchers a partir da camada de aplicação.
///     A Application não referência SignalR: a implementação, no projeto Web,
///     traduz para o LauncherNotifier. É o que mantém o caso de uso testável
///     sem subir um hub.
/// </summary>
public interface IServerHubNotifier
{
    Task NotifyModpackVersionPublishedAsync(Guid modpackId, Guid versionId, CancellationToken ct);
}