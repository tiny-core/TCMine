using TCMine.Contracts.Modpacks;
using TCMine.Contracts.Servers;

namespace TCMine.Launcher.Core.Connectivity;

/// <summary>
///     O canal persistente com o servidor, do ponto de vista de quem usa.
///     Existe para que nada acima desta linha saiba que existe SignalR. A porta
///     acrescenta ao <c>IServerHub</c> duas coisas que o contrato de hub não tem
///     e a aplicação precisa: ciclo de vida (ligar, estado, desligar) e
///     cancelamento.
///     A autenticação NÃO aparece aqui de propósito: o canal usa o mesmo cookie
///     de sessão dos pedidos HTTP, então quem entrou já está autenticado — e
///     passar credencial de novo criaria um segundo caminho para manter em dia.
/// </summary>
public interface IServerConnection : IAsyncDisposable
{
    bool IsConnected { get; }

    /// <summary>Disparado quando o canal cai ou volta, para a UI acompanhar.</summary>
    event Action? StateChanged;

    Task ConnectAsync(Uri serverUrl, CancellationToken ct);

    Task DisconnectAsync();

    Task<IReadOnlyList<ModpackDto>> GetModpacksAsync(CancellationToken ct);

    Task<IReadOnlyList<GameServerDto>> GetServersAsync(CancellationToken ct);

    /// <summary>
    ///     A versão que se deve instalar hoje. Nulo quando o pack ainda não
    ///     publicou nada — resposta legítima, não erro.
    /// </summary>
    Task<ModpackVersionDto?> GetLatestVersionAsync(Guid modpackId, CancellationToken ct);

    /// <summary>O manifesto completo de uma versão. É sobre ele que o diff roda.</summary>
    Task<ModpackVersionDto> GetModpackVersionAsync(Guid versionId, CancellationToken ct);
}
