using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Servers;

namespace TCMine.Server.Web.Background;

/// <summary>
///     Mantém a coluna Status igual ao que os containers estão fazendo, o tempo
///     todo.
///     O container é a fonte da verdade, a coluna é cache. Ela era acertada só
///     no arranque e ao abrir as telas de servidores; um servidor que caía
///     continuava "no ar" para o launcher, e o coletor de métricas (que pula
///     servidor que não está Running) não reparava em nada até alguém olhar.
///     Agora, a cada ligação com o Docker: uma varredura completa — cobre o que
///     aconteceu enquanto não se estava a ouvir, inclusive o reinício do painel
///     — e depois os eventos, um a um. Caiu a ligação, espera e recomeça.
///     Cada status gravado avisa os launchers sozinho (o repositório avisa).
/// </summary>
public sealed partial class ServerStatusReconciler(
    IServiceScopeFactory scopeFactory,
    IContainerEvents events,
    ILogger<ServerStatusReconciler> logger) : BackgroundService
{
    private readonly ILogger<ServerStatusReconciler> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var failures = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using (var scope = scopeFactory.CreateAsyncScope())
                {
                    var corrigidos = await ReconcileAsync(
                        scope.ServiceProvider.GetRequiredService<IServerRepository>(),
                        scope.ServiceProvider.GetRequiredService<IServerOrchestrator>(),
                        scope.ServiceProvider.GetRequiredService<IServerWhitelistSync>(),
                        stoppingToken);

                    if (corrigidos > 0)
                        Corrigiu(corrigidos);
                }

                await foreach (var serverId in events.WatchAsync(stoppingToken))
                {
                    if (failures > 0)
                        Religou();

                    failures = 0;
                    await SyncOneAsync(serverId, stoppingToken);
                }

                // O stream terminou sem erro: o daemon reiniciou. Recomeça já.
                continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Docker fora do ar é comum (o daemon pode subir depois do
                // painel, ou nem existir numa máquina de desenvolvimento). Avisa
                // UMA vez por queda, e não a cada tentativa: o log encheria de
                // uma mesma linha a cada minuto.
                if (failures is 0)
                    Falhou(ex);

                failures++;
            }

            try
            {
                await Task.Delay(RetryDelay(failures), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>De 5 s em 5 s até um minuto: o Docker volta, e ninguém o espera por horas.</summary>
    public static TimeSpan RetryDelay(int failures) => TimeSpan.FromSeconds(Math.Clamp(failures * 5, 5, 60));

    /// <summary>
    ///     Devolve quantos registros estavam desatualizados. Método à parte, e
    ///     não corpo do ExecuteAsync, porque é o que dá para testar sem hospedar
    ///     um serviço de background.
    /// </summary>
    public static async Task<int> ReconcileAsync(
        IServerRepository servers,
        IServerOrchestrator orchestrator,
        IServerWhitelistSync? whitelist,
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
                // dos outros: o que sobrar desatualizado se conserta no próximo
                // evento dele, ou ao abrir a página daquele servidor.
                continue;
            }

            // Sem registro de queda: aqui não se sabe QUANDO caiu, e uma linha
            // de "caiu" com a hora do arranque do painel mentiria. A whitelist
            // sim: um servidor que ficou pronto com o painel fora não a recebeu.
            if (await ServerStatusTransitions.ApplyAsync(server, real, servers, null, whitelist, ct))
                corrigidos++;
        }

        return corrigidos;
    }

    /// <summary>
    ///     Um evento: relê o servidor, pergunta o estado ao orquestrador (a mesma
    ///     regra de sempre) e grava se mudou. Um escopo por evento, como um
    ///     pedido: o serviço vive tanto quanto o painel, e um escopo único
    ///     guardaria tudo o que foi resolvido desde o arranque.
    /// </summary>
    private async Task SyncOneAsync(Guid serverId, CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var servers = scope.ServiceProvider.GetRequiredService<IServerRepository>();

            // Container de um servidor que já foi apagado: nada a acertar.
            if (await servers.GetByIdAsync(serverId, ct) is not { } server)
                return;

            var real = await scope.ServiceProvider.GetRequiredService<IServerOrchestrator>()
                .GetStatusAsync(serverId, ct);

            await ServerStatusTransitions.ApplyAsync(
                server, real, servers,
                scope.ServiceProvider.GetRequiredService<IActivityLogRepository>(),
                scope.ServiceProvider.GetRequiredService<IServerWhitelistSync>(), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Um evento que não se consegue tratar não derruba o acompanhamento
            // dos outros.
            FalhouNoEvento(ex, serverId);
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Status de {Count} servidor(es) corrigido ao ligar ao Docker.")]
    private partial void Corrigiu(int count);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Sem ligação aos eventos do Docker; o status dos servidores fica parado até ela voltar.")]
    private partial void Falhou(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Eventos do Docker de volta.")]
    private partial void Religou();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Não foi possível acertar o status do servidor {ServerId}.")]
    private partial void FalhouNoEvento(Exception ex, Guid serverId);
}
