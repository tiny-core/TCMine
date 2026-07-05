using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using TCMine_Server.Infrastructure.FileSystem;

namespace TCMine_Server.Infrastructure.Server;

/// <summary>
///     Captura métricas instantâneas do HOST/contêiner (CPU, RAM, disco) para os medidores e o gráfico do
///     dashboard. As leituras são <b>globais</b>: CPU de todos os núcleos do host, memória física do
///     sistema/limite de cgroup e uso total do drive — não só do processo do servidor. Singleton: o uptime
///     é medido desde o arranque e a % de CPU precisa de estado entre amostras (compara dois instantes de
///     tempo de CPU acumulado).
/// </summary>
public sealed class SystemMetricsService
{
    // Estado para o cálculo incremental de CPU global. Protegido por lock: Capture() pode ser chamado por
    // circuitos Blazor diferentes (o serviço é singleton, partilhado). Guardamos os totais acumulados da
    // última amostra: idle (ocioso) e total (todos os estados). O uso é o complemento do idle no delta.
    private readonly object _cpuLock = new();

    // Raiz do projeto (content root) — dela derivamos o drive que hospeda a pasta de dados (tcmine-data).
    private readonly string _dataRoot;

    // Estado para o cálculo da taxa de rede (bytes/s). Como o CPU, precisa de dois instantes: guardamos os
    // totais acumulados de bytes (recebidos/enviados) e o momento da última amostra. Protegido por lock
    // (singleton partilhado entre circuitos). No contêiner, reflete as interfaces do próprio contêiner —
    // ou seja, o tráfego real do servidor.
    private readonly object _netLock = new();

    // Marca o momento em que o serviço iniciou (base do uptime)
    private readonly DateTime _startedUtc = DateTime.UtcNow;
    private ulong _lastCpuIdle;
    private ulong _lastCpuTotal;
    private long _lastNetRecv;
    private long _lastNetSent;
    private DateTime _lastNetUtc;

    public SystemMetricsService(string dataRoot)
    {
        _dataRoot = dataRoot;

        // Baseline da CPU: sem uma amostra anterior, a primeira leitura de % sairia distorcida.
        var (idle, total) = ReadCpuTimes();
        _lastCpuIdle = idle;
        _lastCpuTotal = total;

        // Baseline da rede (mesmo motivo: a 1ª taxa precisa de um ponto de partida)
        var (recv, sent) = ReadNetworkTotals();
        _lastNetRecv = recv;
        _lastNetSent = sent;
        _lastNetUtc = DateTime.UtcNow;
    }

    public SystemSnapshot Capture()
    {
        // Process.GetCurrentProcess() lê os contadores do próprio processo do servidor (memória/threads).
        using var p = Process.GetCurrentProcess();

        return new SystemSnapshot(
            // WorkingSet64 = memória física em uso pelo processo do servidor
            p.WorkingSet64,
            // Heap gerenciado pelo GC (subconjunto da memória do processo)
            GC.GetTotalMemory(false),
            p.Threads.Count,
            DateTime.UtcNow - _startedUtc,
            Environment.ProcessorCount,
            CaptureGlobalCpuPercent(),
            CaptureRam(),
            CaptureDisk(),
            CaptureNetwork());
    }

    // ── Rede (taxa de transferência das interfaces) ─────────────────────────────────────────────────────
    // Bytes por segundo (recebidos, enviados) desde a última amostra. Soma todas as interfaces ativas
    // exceto loopback. Cross-platform via NetworkInterface (Windows + Linux). Devolve (0,0) na 1ª amostra
    // ou se o delta de tempo for nulo.
    private (double RecvBytesPerSec, double SentBytesPerSec) CaptureNetwork()
    {
        var (recv, sent) = ReadNetworkTotals();
        var now = DateTime.UtcNow;

        lock (_netLock)
        {
            var seconds = (now - _lastNetUtc).TotalSeconds;
            // Contadores podem resetar (interface reiniciada) → Math.Max evita taxa negativa
            var recvDelta = Math.Max(0, recv - _lastNetRecv);
            var sentDelta = Math.Max(0, sent - _lastNetSent);

            _lastNetRecv = recv;
            _lastNetSent = sent;
            _lastNetUtc = now;

            if (seconds <= 0) return (0, 0);
            return (recvDelta / seconds, sentDelta / seconds);
        }
    }

    // Soma dos bytes acumulados (recebidos, enviados) das interfaces ativas (sem loopback). Plataformas/
    // permissões sem suporte devolvem (0,0) → taxa fica em 0 em vez de derrubar o dashboard.
    private static (long Recv, long Sent) ReadNetworkTotals()
    {
        try
        {
            long recv = 0, sent = 0;
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                var stats = nic.GetIPStatistics(); // IPv4 + IPv6 combinados
                recv += stats.BytesReceived;
                sent += stats.BytesSent;
            }

            return (recv, sent);
        }
        catch
        {
            return (0, 0);
        }
    }

    // ── CPU (global do host) ──────────────────────────────────────────────────────────────────────────
    // % de uso de CPU de TODOS os núcleos do host desde a última amostra (0–100). Cross-platform sem
    // depender de PerformanceCounter/WMI: no Linux lê os contadores acumulados de /proc/stat (que no
    // contêiner refletem o host — o accounting de CPU não é isolado por namespace); no Windows usa
    // GetSystemTimes. Em ambos, o uso é (totalDelta − idleDelta) / totalDelta.
    private double CaptureGlobalCpuPercent()
    {
        var (idle, total) = ReadCpuTimes();

        lock (_cpuLock)
        {
            // Contadores são monotônicos, então os deltas nunca "voltam" (subtração de ulong segura)
            var idleDelta = idle - _lastCpuIdle;
            var totalDelta = total - _lastCpuTotal;

            _lastCpuIdle = idle;
            _lastCpuTotal = total;

            if (totalDelta == 0) return 0;

            var busy = (double)(totalDelta - idleDelta) / totalDelta * 100d;
            return Math.Clamp(busy, 0, 100);
        }
    }

    // Lê os tempos acumulados de CPU do host: (idle, total). Plataformas sem suporte devolvem (0,0),
    // o que faz a % ficar em 0 (medidor apagado) em vez de derrubar o dashboard.
    private static (ulong Idle, ulong Total) ReadCpuTimes()
    {
        if (OperatingSystem.IsWindows()) return ReadWindowsCpuTimes();
        if (OperatingSystem.IsLinux()) return ReadLinuxCpuTimes();
        return (0, 0);
    }

    // /proc/stat primeira linha: "cpu user nice system idle io wait irq softirq steal guest guest_nice".
    // idle = idle + iowait; total = soma de todos os campos numéricos.
    private static (ulong Idle, ulong Total) ReadLinuxCpuTimes()
    {
        try
        {
            var line = File.ReadLines("/proc/stat")
                .FirstOrDefault(l => l.StartsWith("cpu ", StringComparison.Ordinal));
            if (line is null) return (0, 0);

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            ulong idle = 0, total = 0;
            // parts[0] == "cpu"; os campos numéricos começam em parts[1]
            for (var i = 1; i < parts.Length; i++)
            {
                if (!ulong.TryParse(parts[i], out var v)) continue;
                total += v;
                if (i is 4 or 5) idle += v; // idle (4) + iowait (5)
            }

            return (idle, total);
        }
        catch
        {
            return (0, 0); // /proc indisponível → sem métrica de CPU
        }
    }

    // GetSystemTimes devolve tempos acumulados do sistema (100 ns). kernel INCLUI o idle, então
    // total = kernel + user e idle é o próprio idle.
    private static (ulong Idle, ulong Total) ReadWindowsCpuTimes()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
            return (0, 0);

        var i = ToUInt64(idle);
        var k = ToUInt64(kernel);
        var u = ToUInt64(user);
        return (i, k + u);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FILETIME idleTime, out FILETIME kernelTime, out FILETIME userTime);

    private static ulong ToUInt64(FILETIME f)
    {
        return ((ulong)(uint)f.dwHighDateTime << 32) | (uint)f.dwLowDateTime;
    }

    // ── RAM (global do host/contêiner) ────────────────────────────────────────────────────────────────
    // Memória física do sistema/contêiner via GC. GetGCMemoryInfo() reflete os limites do Docker
    // (TotalAvailableMemoryBytes = limite físico/cgroup; MemoryLoadBytes = física em uso no host).
    private static (long Used, long Total) CaptureRam()
    {
        var info = GC.GetGCMemoryInfo();
        return (info.MemoryLoadBytes, info.TotalAvailableMemoryBytes);
    }

    // ── Disco (global do drive) ───────────────────────────────────────────────────────────────────────
    // Uso e capacidade do drive inteiro que hospeda os dados do projeto (não só a pasta tcmine-data).
    // DriveInfo é barato, então lemos a cada Capture sem cache.
    private (long Used, long Total) CaptureDisk()
    {
        try
        {
            var dataDir = ServerPaths.Data(_dataRoot);
            var root = Path.GetPathRoot(Path.GetFullPath(dataDir));
            if (!string.IsNullOrEmpty(root) && new DriveInfo(root) is { IsReady: true } drive)
                return (drive.TotalSize - drive.AvailableFreeSpace, drive.TotalSize);
        }
        catch
        {
            // Drive indisponível → devolve zeros (medidor apagado) em vez de derrubar o dashboard
        }

        return (0, 0);
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
    (long Used, long Total) Disk,
    (double RecvBytesPerSec, double SentBytesPerSec) Network)
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

    // Rede em MB/s (recebido, enviado e total) — a base do medidor e do gráfico
    public double NetRecvMbps => Network.RecvBytesPerSec / 1024d / 1024d;
    public double NetSentMbps => Network.SentBytesPerSec / 1024d / 1024d;
    public double NetTotalMbps => NetRecvMbps + NetSentMbps;
}