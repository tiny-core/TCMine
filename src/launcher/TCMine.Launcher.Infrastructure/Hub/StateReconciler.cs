using Microsoft.Extensions.Logging;
using TCMine.Contracts.Servers;

namespace TCMine.Launcher.Infrastructure.Hub;

/// <summary>
///     Traz o estado local de volta à verdade do servidor.
///     Roda ao conectar, ao reconectar e quando a janela ganha foco. É a rede de
///     segurança que torna o push do SignalR uma otimização em vez de um
///     requisito: se um evento se perdeu durante uma queda de conexão, a
///     próxima reconciliação corrige a divergência.
/// </summary>
public sealed partial class StateReconciler(
    LauncherHubClient hub,
    ILogger<StateReconciler> logger)
{
    private readonly ILogger<StateReconciler> _logger = logger;

    /// <summary>Emite a lista de servidores atual, para a UI se atualizar.</summary>
    public event Action<IReadOnlyList<GameServerDto>>? ServersRefreshed;

    public async Task ReconcileAsync(CancellationToken ct)
    {
        try
        {
            // Puxa o estado inteiro em vez de aplicar deltas. Mais simples e
            // sem risco de acumular divergência: o que o servidor diz agora
            // é a verdade, ponto.
            var servers = await hub.GetServersAsync();

            ServersRefreshed?.Invoke(servers);

            LogReconciled(servers.Count);
        }
        catch (Exception ex)
        {
            // Falha na reconciliação não derruba nada: a próxima tentativa
            // (foco, reconexão) roda de novo. O jogador continua vendo o
            // último estado conhecido.
            LogReconcileFailed(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Estado reconciliado: {Count} servidores.")]
    private partial void LogReconciled(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao reconciliar estado.")]
    private partial void LogReconcileFailed(Exception ex);
}
