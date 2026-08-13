using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Infrastructure.Docker;

/// <summary>
///     Lê /containers/{id}/stats com <c>stream=false</c>.
///     O endpoint tem dois modos: um stream contínuo (uma amostra por segundo,
///     para sempre) e um retrato único. Usamos o retrato — manter um stream vivo
///     por container gastaria conexão e thread para dados que a tela lê a cada 15
///     segundos. O preço é que o Docker espera ~1s internamente para calcular o
///     delta de CPU, então a chamada não é instantânea.
/// </summary>
public sealed partial class DockerContainerStats(
    DockerApiClient docker,
    ILogger<DockerContainerStats> logger) : IContainerStats
{
    private readonly ILogger<DockerContainerStats> _logger = logger;

    public async Task<ContainerSample?> SampleAsync(Guid gameServerId, CancellationToken ct)
    {
        try
        {
            var raw = await docker.GetStatsAsync($"tcmine-{gameServerId}", ct);
            if (raw?.CpuStats is null || raw.MemoryStats is null)
                return null;

            return new ContainerSample(
                CpuPercentOf(raw),
                raw.MemoryStats.Usage - (raw.MemoryStats.Stats?.GetValueOrDefault("inactive_file") ?? 0),
                raw.MemoryStats.Limit);
        }
        catch (HttpRequestException ex)
        {
            // Daemon fora do ar ou container removido no meio: telemetria não
            // pode derrubar o coletor.
            LogSampleFailed(ex, gameServerId);
            return null;
        }
    }

    /// <summary>
    ///     A conta oficial do Docker: variação de ciclos usados pelo container
    ///     sobre a variação de ciclos disponíveis no sistema, vezes o número de
    ///     CPUs. Sem multiplicar pelos núcleos, um container saturando 4 CPUs
    ///     apareceria como 25%.
    /// </summary>
    private static double CpuPercentOf(DockerStatsResponse raw)
    {
        var cpu = raw.CpuStats!;
        var pre = raw.PreCpuStats;

        if (pre?.CpuUsage is null || cpu.CpuUsage is null)
            return 0;

        var cpuDelta = (double)(cpu.CpuUsage.TotalUsage - pre.CpuUsage.TotalUsage);
        var systemDelta = (double)(cpu.SystemCpuUsage - pre.SystemCpuUsage);

        if (cpuDelta <= 0 || systemDelta <= 0)
            return 0;

        // OnlineCpus vem zerado em daemons antigos; cair para o tamanho do array
        // de percpu é o fallback documentado.
        var cpus = cpu.OnlineCpus > 0
            ? cpu.OnlineCpus
            : cpu.CpuUsage.PerCpuUsage?.Count ?? 1;

        return Math.Clamp(cpuDelta / systemDelta * cpus * 100d, 0, 100 * cpus);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Não foi possível amostrar o container do servidor {ServerId}.")]
    private partial void LogSampleFailed(Exception ex, Guid serverId);
}

internal sealed record DockerStatsResponse
{
    [JsonPropertyName("cpu_stats")] public DockerCpuStats? CpuStats { get; init; }
    [JsonPropertyName("precpu_stats")] public DockerCpuStats? PreCpuStats { get; init; }
    [JsonPropertyName("memory_stats")] public DockerMemoryStats? MemoryStats { get; init; }
}

internal sealed record DockerCpuStats
{
    [JsonPropertyName("cpu_usage")] public DockerCpuUsage? CpuUsage { get; init; }
    [JsonPropertyName("system_cpu_usage")] public ulong SystemCpuUsage { get; init; }
    [JsonPropertyName("online_cpus")] public int OnlineCpus { get; init; }
}

internal sealed record DockerCpuUsage
{
    [JsonPropertyName("total_usage")] public ulong TotalUsage { get; init; }
    [JsonPropertyName("percpu_usage")] public IReadOnlyList<ulong>? PerCpuUsage { get; init; }
}

internal sealed record DockerMemoryStats
{
    [JsonPropertyName("usage")] public long Usage { get; init; }
    [JsonPropertyName("limit")] public long Limit { get; init; }

    /// <summary>
    ///     Contadores do cgroup. Descontamos "inactive_file" porque cache de
    ///     página entra em "usage" e faria todo servidor parecer no limite.
    /// </summary>
    [JsonPropertyName("stats")]
    public IReadOnlyDictionary<string, long>? Stats { get; init; }
}
