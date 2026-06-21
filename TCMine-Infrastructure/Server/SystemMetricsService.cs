using System.Diagnostics;

namespace TCMine_Infrastructure.Server;

/// <summary>
/// Captura métricas instantâneas do processo do servidor para o gráfico de status
/// do dashboard. Singleton: o tempo de atividade é medido desde o arranque do aplicativo.
/// </summary>
public sealed class SystemMetricsService
{
    // Marca o momento em que o serviço iniciou
    private readonly DateTime _startedUtc = DateTime.UtcNow;

    public SystemSnapshot Capture()
    {
        // Process.GetCurrentProcess() lê os contadores atuais do próprio processo
        using var p = Process.GetCurrentProcess();

        return new SystemSnapshot(
            // WorkingSet64 = memória física em uso pelo processo
            p.WorkingSet64 / 1024d / 1024d,
            // Heap gerenciado pelo GC (subconjunto da memória total)
            GC.GetTotalMemory(false) / 1024d / 1024d,
            p.Threads.Count,
            DateTime.UtcNow - _startedUtc);
    }
}

/// <summary>Fotografia das métricas do processo em um instante.</summary>
public readonly record struct SystemSnapshot(
    double MemoryMb,
    double ManagedHeapMb,
    int Threads,
    TimeSpan Uptime);