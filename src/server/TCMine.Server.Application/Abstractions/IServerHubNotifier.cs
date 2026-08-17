using TCMine.Contracts.Hubs;

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

    /// <summary>
    ///     Uma linha do console para quem acompanha o servidor.
    ///     Passa por esta porta, e não pelo LauncherNotifier direto, pelo motivo
    ///     que a interface inteira existe: quem bombeia o console precisa ser
    ///     testável sem subir um hub, e o teste que importa ali é de contagem de
    ///     streams abertos — não de entrega de mensagem.
    /// </summary>
    Task NotifyConsoleLineAsync(Guid serverId, ConsoleLineDto line, CancellationToken ct);

    /// <summary>
    ///     A contagem de jogadores mudou. Só é chamado na mudança, não a cada
    ///     amostragem: repetir o mesmo número para todo launcher conectado a
    ///     cada quinze segundos é tráfego que não informa nada.
    /// </summary>
    Task NotifyPlayerCountChangedAsync(Guid serverId, int online, int max, CancellationToken ct);
}
