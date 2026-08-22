using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Web.Background;

/// <summary>
///     No arranque, acerta a coluna Status com o que os containers realmente
///     estão fazendo.
///     O container é a fonte da verdade, a coluna é cache — e o cache não
///     sobrevive a um reinício do painel: os containers sobem com
///     <c>unless-stopped</c> e continuam de pé enquanto a coluna guarda o que
///     valia antes do desligamento. A página conserta ao ser aberta, mas o que
///     roda sozinho não espera ninguém abrir página: o coletor de métricas pula
///     servidor que não está marcado como Running, então um servidor no ar
///     ficaria sem gráfico e sem contagem de jogadores até alguém visitá-lo.
/// </summary>
public sealed partial class ServerStatusReconciler(
    IServiceScopeFactory scopeFactory,
    ILogger<ServerStatusReconciler> logger) : BackgroundService
{
    private readonly ILogger<ServerStatusReconciler> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            var corrigidos = await ReconcileAsync(
                scope.ServiceProvider.GetRequiredService<IServerRepository>(),
                scope.ServiceProvider.GetRequiredService<IServerOrchestrator>(),
                stoppingToken);

            if (corrigidos > 0)
                Corrigiu(corrigidos);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Desligando antes de terminar: sem problema, a próxima subida faz.
        }
        catch (Exception ex)
        {
            // Docker fora do ar no arranque é comum (o daemon pode subir depois
            // do painel). Falhar aqui não pode impedir a aplicação de servir —
            // o status volta a ser corrigido quando a página for aberta.
            Falhou(ex);
        }
    }

    /// <summary>
    ///     Devolve quantos registros estavam desatualizados. Método à parte, e
    ///     não corpo do ExecuteAsync, porque é o que dá para testar sem hospedar
    ///     um serviço de background.
    /// </summary>
    public static async Task<int> ReconcileAsync(
        IServerRepository servers,
        IServerOrchestrator orchestrator,
        CancellationToken ct)
    {
        var corrigidos = 0;

        foreach (var server in await servers.ListAllAsync(ct))
        {
            GameServerStatus real;
            try
            {
                real = await orchestrator.GetStatusAsync(server.Id, ct);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // Um container que não responde não pode interromper a varredura
                // dos outros: o que sobrar desatualizado se conserta ao abrir a
                // página daquele servidor.
                continue;
            }

            if (server.Status == real)
                continue;

            server.Status = real;
            await servers.UpdateAsync(server, ct);
            corrigidos++;
        }

        return corrigidos;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Status de {Count} servidor(es) corrigido no arranque.")]
    private partial void Corrigiu(int count);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Não foi possível reconciliar o status dos servidores no arranque.")]
    private partial void Falhou(Exception ex);
}
