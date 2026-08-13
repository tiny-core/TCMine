using System.Diagnostics;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Web.Background;

/// <summary>
///     Amostra consumo a cada intervalo e alimenta o histórico.
///     Roda em background, não sob demanda da tela: se cada admin que abrisse o
///     painel disparasse a coleta, dez abas abertas seriam dez vezes o trabalho,
///     e o gráfico começaria vazio a cada visita. Assim a série já existe quando
///     alguém chega.
/// </summary>
public sealed partial class MetricsCollector(
    MetricsHistory history,
    IServiceScopeFactory scopeFactory,
    ILogger<MetricsCollector> logger) : BackgroundService
{
    /// <summary>
    ///     15s é o compromisso: o /stats do Docker espera ~1s internamente por
    ///     container para calcular o delta de CPU, então amostrar de 2 em 2
    ///     segundos com dez servidores gastaria mais tempo coletando do que
    ///     parado — e ninguém olha um gráfico de servidor com essa granularidade.
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    private readonly ILogger<MetricsCollector> _logger = logger;

    private TimeSpan _lastCpuTime = TimeSpan.Zero;
    private DateTimeOffset _lastSampleAt = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CollectAsync(stoppingToken);
                history.Publish();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Telemetria com erro nunca pode derrubar o serviço: o painel
                // perde o gráfico, o servidor de jogo segue rodando.
                LogCollectFailed(ex);
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
                break;
        }
    }

    private async Task CollectAsync(CancellationToken ct)
    {
        history.AddHost(SampleHost());

        await using var scope = scopeFactory.CreateAsyncScope();
        var servers = await scope.ServiceProvider
            .GetRequiredService<IServerRepository>()
            .ListAllAsync(ct);

        var stats = scope.ServiceProvider.GetRequiredService<IContainerStats>();

        foreach (var server in servers)
        {
            // Servidor parado não tem container para amostrar; grava um ponto
            // zerado para o gráfico mostrar a queda em vez de um buraco.
            if (server.Status is not GameServerStatus.Running)
            {
                history.AddServer(server.Id, new MetricPoint(DateTimeOffset.UtcNow, 0, 0, 0));
                continue;
            }

            var sample = await stats.SampleAsync(server.Id, ct);
            history.AddServer(server.Id, new MetricPoint(
                DateTimeOffset.UtcNow,
                sample?.CpuPercent ?? 0,
                sample?.MemoryUsedBytes ?? 0,
                sample?.MemoryLimitBytes ?? 0));
        }
    }

    /// <summary>
    ///     CPU do processo por diferença de tempo consumido entre duas coletas —
    ///     não existe leitura instantânea. A primeira coleta sai zerada porque
    ///     não há de quê subtrair.
    /// </summary>
    private HostPoint SampleHost()
    {
        var process = Process.GetCurrentProcess();
        var now = DateTimeOffset.UtcNow;
        var cpuTime = process.TotalProcessorTime;

        double cpuPercent = 0;
        if (_lastSampleAt != DateTimeOffset.MinValue)
        {
            var wall = (now - _lastSampleAt).TotalMilliseconds;
            var used = (cpuTime - _lastCpuTime).TotalMilliseconds;

            if (wall > 0)
                cpuPercent = Math.Clamp(used / (wall * Environment.ProcessorCount) * 100d, 0, 100);
        }

        _lastSampleAt = now;
        _lastCpuTime = cpuTime;

        var (free, total) = DiskOf(AppContext.BaseDirectory);

        return new HostPoint(now, cpuPercent, process.WorkingSet64, free, total);
    }

    private static (long Free, long Total) DiskOf(string path)
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(path) ?? "/");
            return (drive.AvailableFreeSpace, drive.TotalSize);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            // Caminho em volume que o runtime não sabe inspecionar (bind mount
            // exótico): melhor não mostrar disco do que mostrar número errado.
            return (0, 0);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao coletar métricas.")]
    private partial void LogCollectFailed(Exception ex);
}
