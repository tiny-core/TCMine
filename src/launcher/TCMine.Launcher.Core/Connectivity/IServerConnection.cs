using TCMine.Contracts.Hubs;
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

    /// <summary>O estado do canal neste instante. Ver <see cref="LinkStatus" />.</summary>
    LinkStatus Status { get; }

    /// <summary>
    ///     O canal está em baixo porque o servidor recusou a sessão (ela expirou
    ///     ou foi revogada), e não por falta de rede.
    ///     Tentar de novo com a mesma sessão dá no mesmo, por isso o canal para
    ///     de tentar sozinho: quem tem de agir é quem sabe renovar a sessão, e
    ///     depois chamar <see cref="KeepConnected" /> outra vez.
    /// </summary>
    bool SessionRejected { get; }

    /// <summary>
    ///     Disparado a cada mudança de <see cref="Status" /> — inclusive quando o
    ///     canal cai sozinho e quando volta. Pode vir de qualquer thread.
    /// </summary>
    event Action? StateChanged;

    /// <summary>
    ///     Mantém o canal aberto com este servidor, tentando de novo sem limite
    ///     sempre que ele estiver em baixo.
    ///     Não espera e não lança: quem chama está a declarar uma intenção
    ///     ("quero estar ligado"), e o resultado chega por <see cref="StateChanged" />.
    ///     Chamar outra vez com o mesmo endereço não faz nada — a não ser que a
    ///     sessão tivesse sido recusada (<see cref="SessionRejected" />): aí é o
    ///     sinal de que ela foi renovada, e as tentativas recomeçam. Vale até
    ///     <see cref="DisconnectAsync" />.
    /// </summary>
    void KeepConnected(Uri serverUrl);

    /// <summary>
    ///     O servidor avisou que algo que o launcher mostra mudou (ver
    ///     <c>ILauncherClient.Invalidated</c>). Quem guarda esse dado rebusca.
    ///     Pode vir de qualquer thread. Um tipo que este launcher não conhece
    ///     (de um servidor mais novo) chega na mesma, e quem ouve o ignora.
    /// </summary>
    event Action<ResourceKind, Guid?>? Invalidated;

    Task ConnectAsync(Uri serverUrl, CancellationToken ct);

    Task DisconnectAsync();

    Task<IReadOnlyList<ModpackDto>> GetModpacksAsync(CancellationToken ct);

    Task<IReadOnlyList<GameServerDto>> GetServersAsync(CancellationToken ct);

    /// <summary>
    ///     A versão que se deve instalar hoje. Nulo quando o pack ainda não
    ///     publicou nada — resposta legítima, não erro.
    /// </summary>
    Task<ModpackVersionDto?> GetLatestVersionAsync(
        Guid modpackId, ReleaseChannel channel, CancellationToken ct);

    /// <summary>
    ///     O histórico instalável, da mais nova para a mais velha. Sem os
    ///     arquivos — instalar continua a pedir o manifesto por versão.
    /// </summary>
    Task<IReadOnlyList<ModpackVersionSummaryDto>> GetVersionsAsync(
        Guid modpackId, ReleaseChannel channel, CancellationToken ct);

    /// <summary>Novidades publicadas do pack, da mais recente para a mais antiga.</summary>
    Task<IReadOnlyList<ModpackNewsDto>> GetNewsAsync(Guid modpackId, CancellationToken ct);

    /// <summary>O manifesto completo de uma versão. É sobre ele que o diff roda.</summary>
    Task<ModpackVersionDto> GetModpackVersionAsync(Guid versionId, CancellationToken ct);

    /// <summary>
    ///     Pede acesso a um servidor com whitelist. Lança se o servidor já tem
    ///     dono recusando (já tem acesso, não existe) — a tela trata como
    ///     qualquer outra falha de ação, não como desfecho normal.
    /// </summary>
    Task RequestServerAccessAsync(Guid gameServerId, CancellationToken ct);
}
