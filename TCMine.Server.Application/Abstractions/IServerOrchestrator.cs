using TCMine.Contracts.Servers;

namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Ciclo de vida das instâncias de Minecraft.
///     A implementação vai falar com a API HTTP do Docker via HttpClient sobre
///     UnixDomainSocketEndPoint, subindo containers itzg/minecraft-server.
///     Regra que atravessa o desenho: as instâncias são containers
///     próprios, NUNCA processos filhos do TCMine. Atualizar o painel não pode
///     derrubar quem está jogando.
/// </summary>
public interface IServerOrchestrator
{
    /// <summary>Cria o container se ainda não existe. Devolve o ID.</summary>
    Task<string> EnsureCreatedAsync(Guid gameServerId, CancellationToken ct);

    Task StartAsync(Guid gameServerId, CancellationToken ct);

    /// <summary>
    ///     Parada graciosa. Manda SIGTERM e espera: o stop-server.sh do itzg
    ///     salva o mundo antes de sair. Matar o processo direto corrompe chunks,
    ///     então o timeout precisa ser generoso.
    /// </summary>
    Task StopAsync(Guid gameServerId, TimeSpan timeout, CancellationToken ct);

    Task<GameServerStatus> GetStatusAsync(Guid gameServerId, CancellationToken ct);

    /// <summary>
    ///     Stream contínuo do console. IAsyncEnumerable porque as linhas chegam
    ///     ao longo do tempo e não faz sentido acumular tudo em memória antes de
    ///     repassar ao Hub.
    /// </summary>
    IAsyncEnumerable<string> StreamLogsAsync(Guid gameServerId, CancellationToken ct);
}

/// <summary>
///     Envio de comandos ao servidor de jogo.
///     Existe separado do orquestrador porque a senha do RCON é o segredo mais
///     sensível do sistema. Quem chama aqui já passou pela autorização — esta
///     interface confia no chamador e não valida permissão.
/// </summary>
public interface IRconClient
{
    Task<string> ExecuteAsync(Guid gameServerId, string rawCommand, CancellationToken ct);
}