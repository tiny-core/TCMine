using System.Diagnostics;
using TCMine_Server.Infrastructure.FileSystem;

namespace TCMine_Server.Infrastructure.Server;

/// <summary>
/// Captura métricas instantâneas do processo do servidor E do host/contêiner (CPU, RAM, disco)
/// para os medidores e o gráfico do dashboard. Singleton: o tempo de atividade é medido desde o
/// arranque e a % de CPU precisa de estado entre amostras (compara dois instantes de tempo de CPU).
/// </summary>
public sealed class SystemMetricsService
{
    // Marca o momento em que o serviço iniciou (base do uptime)
    private readonly DateTime _startedUtc = DateTime.UtcNow;

    // Raiz do projeto (content root) — dela derivamos a pasta de dados (tcmine-data) e o drive.
    private readonly string _dataRoot;

    // Estado para o cálculo incremental de CPU. Protegido por lock: Capture() pode ser
    // chamado por circuitos Blazor diferentes (o serviço é singleton, partilhado).
    private readonly object _cpuLock = new();
    private DateTime _lastCpuSampleUtc;
    private TimeSpan _lastCpuTotal;

    // Cache do uso de disco: varrer a pasta de dados recursivamente é caro e o Capture() roda a cada
    // 2s. Recalculamos no máximo a cada DiskSampleInterval; entre amostras, devolvemos o último valor.
    private static readonly TimeSpan DiskSampleInterval = TimeSpan.FromSeconds(30);
    private readonly object _diskLock = new();
    private DateTime _lastDiskSampleUtc = DateTime.MinValue;
    private long _cachedDataUsed;
    private long _cachedDiskTotal;

    public SystemMetricsService(string dataRoot)
    {
        _dataRoot = dataRoot;

        // Baseline da CPU: sem uma amostra anterior, a primeira leitura de % sairia distorcida.
        using var p = Process.GetCurrentProcess();
        _lastCpuSampleUtc = DateTime.UtcNow;
        _lastCpuTotal = p.TotalProcessorTime;
    }

    public SystemSnapshot Capture()
    {
        // Process.GetCurrentProcess() lê os contadores atuais do próprio processo
        using var p = Process.GetCurrentProcess();

        return new SystemSnapshot(
            // WorkingSet64 = memória física em uso pelo processo
            p.WorkingSet64,
            // Heap gerenciado pelo GC (subconjunto da memória do processo)
            GC.GetTotalMemory(false),
            p.Threads.Count,
            DateTime.UtcNow - _startedUtc,
            Environment.ProcessorCount,
            CaptureCpuPercent(p),
            CaptureRam(),
            CaptureDisk());
    }

    // ── CPU ──────────────────────────────────────────────────────────────────────────────────────
    // % de CPU consumida pelo processo desde a última amostra, normalizada pelo nº de núcleos
    // (0–100%). Abordagem cross-platform (não depende de PerformanceCounter/WMI, que são Windows-only):
    // compara o tempo total de CPU gasto contra o tempo de relógio decorrido × núcleos.
    private double CaptureCpuPercent(Process p)
    {
        lock (_cpuLock)
        {
            var nowUtc = DateTime.UtcNow;
            var nowCpu = p.TotalProcessorTime;

            var wallMs = (nowUtc - _lastCpuSampleUtc).TotalMilliseconds;
            var cpuMs = (nowCpu - _lastCpuTotal).TotalMilliseconds;

            _lastCpuSampleUtc = nowUtc;
            _lastCpuTotal = nowCpu;

            if (wallMs <= 0)
                return 0;

            // Divide pelo nº de núcleos: 100% = todos os núcleos saturados por este processo
            var pct = cpuMs / (wallMs * Environment.ProcessorCount) * 100d;
            return Math.Clamp(pct, 0, 100);
        }
    }

    // ── RAM ──────────────────────────────────────────────────────────────────────────────────────
    // Memória física do sistema/contêiner via GC. GetGCMemoryInfo() reflete os limites do Docker
    // (TotalAvailableMemoryBytes = limite físico/cgroup; MemoryLoadBytes = física em uso no host).
    private static (long Used, long Total) CaptureRam()
    {
        var info = GC.GetGCMemoryInfo();
        return (info.MemoryLoadBytes, info.TotalAvailableMemoryBytes);
    }

    // ── Disco ────────────────────────────────────────────────────────────────────────────────────
    // Uso = tamanho ocupado APENAS pelos dados do projeto (tcmine-data), não o drive inteiro.
    // Total = capacidade do drive que hospeda esses dados → o medidor mostra a fatia do disco que o
    // projeto consome. Cacheado (DiskSampleInterval), pois a varredura recursiva é cara.
    private (long Used, long Total) CaptureDisk()
    {
        lock (_diskLock)
        {
            var now = DateTime.UtcNow;
            if (now - _lastDiskSampleUtc < DiskSampleInterval)
                return (_cachedDataUsed, _cachedDiskTotal);

            _lastDiskSampleUtc = now;

            try
            {
                var dataDir = ServerPaths.Data(_dataRoot);
                _cachedDataUsed = DirectorySizeBytes(dataDir);

                var root = Path.GetPathRoot(Path.GetFullPath(dataDir));
                _cachedDiskTotal = !string.IsNullOrEmpty(root) && new DriveInfo(root) is { IsReady: true } drive
                    ? drive.TotalSize
                    : 0;
            }
            catch
            {
                // Pasta/drive indisponível → mantém o último valor conhecido em vez de derrubar o dashboard
            }

            return (_cachedDataUsed, _cachedDiskTotal);
        }
    }

    // Soma o tamanho dos arquivos sob 'path' (recursivo, iterativo para não estourar a pilha). Pula
    // reparse points (symlink/junction): o cache de runtime do servidor é linkado nas instâncias, então
    // segui-los inflaria o total (contagem dupla) e poderia criar loops.
    private static long DirectorySizeBytes(string path)
    {
        var root = new DirectoryInfo(path);
        if (!root.Exists) return 0;

        long total = 0;
        var stack = new Stack<DirectoryInfo>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var dir = stack.Pop();

            FileSystemInfo[] entries;
            try { entries = dir.GetFileSystemInfos(); }
            catch { continue; } // sem permissão / pasta removida durante a varredura

            foreach (var entry in entries)
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) continue; // ignora links

                if (entry is DirectoryInfo sub) stack.Push(sub);
                else if (entry is FileInfo file) total += file.Length;
            }
        }

        return total;
    }
}

/// <summary>Fotografia das métricas do processo e do host em um instante.</summary>
public readonly record struct SystemSnapshot(
    long WorkingSetBytes,
    long ManagedHeapBytes,
    int Threads,
    TimeSpan Uptime,
    int ProcessorCount,
    double CpuPercent,
    (long Used, long Total) Ram,
    (long Used, long Total) Disk)
{
    // Conveniências de apresentação — mantêm a UI livre de conversões repetidas
    public double WorkingSetMb => WorkingSetBytes / 1024d / 1024d;
    public double ManagedHeapMb => ManagedHeapBytes / 1024d / 1024d;

    public double RamUsedGb => Ram.Used / 1024d / 1024d / 1024d;
    public double RamTotalGb => Ram.Total / 1024d / 1024d / 1024d;
    public double RamPercent => Ram.Total > 0 ? Ram.Used / (double)Ram.Total * 100d : 0;

    public double DiskUsedGb => Disk.Used / 1024d / 1024d / 1024d;
    public double DiskTotalGb => Disk.Total / 1024d / 1024d / 1024d;
    public double DiskPercent => Disk.Total > 0 ? Disk.Used / (double)Disk.Total * 100d : 0;

    // Uso dos dados do projeto em unidade adaptativa (MB abaixo de 1 GB) — costuma ser pequeno
    public string DiskUsedLabel => Disk.Used >= 1L << 30
        ? $"{DiskUsedGb:0.0} GB"
        : $"{Disk.Used / 1024d / 1024d:0} MB";
}
