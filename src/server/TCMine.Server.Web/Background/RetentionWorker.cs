using TCMine.Server.Application.Storage;

namespace TCMine.Server.Web.Background;

/// <summary>
///     Roda a retenção (<see cref="RunRetention" />) uma vez por dia. A primeira
///     rodada espera o arranque assentar (migrations, Docker, servidores
///     voltando), e depois é de 24 em 24 horas a partir dela — não num horário
///     fixo: apagar em blocos curtos não pesa a ponto de precisar de madrugada.
///     Uma rodada que falha (banco fora do ar) só vai para o log; a seguinte
///     tenta tudo de novo, porque cada rodada recalcula o que venceu.
/// </summary>
public sealed partial class RetentionWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<RetentionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(10);

    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
            using var timer = new PeriodicTimer(Interval);
            do
            {
                await RunOnceAsync(stoppingToken);
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Desligando: a próxima subida faz a rodada.
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<RunRetention>().HandleAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogFailed(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Retenção: a rodada falhou; a próxima tenta de novo.")]
    private partial void LogFailed(Exception ex);
}
