using System.Collections.Concurrent;
using Docker.DotNet.Models;
using Microsoft.Extensions.Hosting;
using TCMine_Server.Infrastructure.FileSystem;

namespace TCMine_Server.Infrastructure.ServerInstances;

/// <summary>
///     Métricas de runtime por instância de servidor: CPU/RAM ao vivo dos containers (lidas direto do daemon
///     Docker) e uso em disco do diretório de cada instância. Singleton e sem BD: fala com o
///     <see cref="DockerEnvironment" /> (thread-safe) e com o filesystem, então pode ser amostrado por um Timer
///     do dashboard sem disputar o DbContext scoped de um circuito Blazor. Os metadados da instância (nome,
///     RAM configurada, status) vêm de outra fonte e são cruzados por Id.
/// </summary>
public sealed class ServerInstanceMetricsService(DockerEnvironment docker, IHostEnvironment env)
{
    // Prefixo de nome dos containers das instâncias (ver DockerMinecraftManager.ContainerName)
    private const string NamePrefix = "tcmine-mc-";

    // Uso em disco é caro de medir (varredura recursiva) e muda devagar → cacheado por instância e
    // recalculado no máximo a cada DiskSampleInterval. Concurrent: o Timer do dashboard pode reamostrar
    // enquanto uma varredura anterior ainda escreve.
    private static readonly TimeSpan DiskSampleInterval = TimeSpan.FromSeconds(30);
    private readonly ConcurrentDictionary<Guid, (DateTime At, long Bytes)> _diskCache = new();

    // Raiz dos diretórios das instâncias (tcmine-data/servers). O tamanho em disco de cada uma é o
    // tamanho da sua subpasta {id}.
    private readonly string _serversRoot = ServerPaths.Servers(env.ContentRootPath);

    /// <summary>
    ///     Amostra as métricas de todos os containers de instância em execução, indexadas pelo Id da
    ///     instância. Instâncias paradas (sem container rodando) simplesmente não aparecem no dicionário.
    ///     Uma falha de amostragem de um container é ignorada (ele fica de fora) — não derruba as demais.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, ServerInstanceStats>> SampleAsync(CancellationToken ct = default)
    {
        IList<ContainerListResponse> containers;
        try
        {
            containers = await docker.Client.Containers.ListContainersAsync(new ContainersListParameters
            {
                // Só os que estão de pé — stats de container parado não faz sentido
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["name"] = new Dictionary<string, bool> { [NamePrefix] = true },
                    ["status"] = new Dictionary<string, bool> { ["running"] = true }
                }
            }, ct);
        }
        catch
        {
            return new Dictionary<Guid, ServerInstanceStats>(); // daemon indisponível → sem métricas
        }

        // Amostra cada container em paralelo: GetContainerStats faz uma leitura bloqueante de ~1s (o
        // daemon precisa de duas coletas para o delta de CPU), então serializar seria lento com N servidores.
        var samples = await Task.WhenAll(containers.Select(c => SampleOneAsync(c, ct)));

        var result = new Dictionary<Guid, ServerInstanceStats>();
        foreach (var s in samples)
            if (s is { } stat)
                result[stat.InstanceId] = stat;

        return result;
    }

    /// <summary>
    ///     Uso em disco (bytes) do diretório de cada instância pedida, indexado por Id. Aplica-se a QUALQUER
    ///     instância (rodando ou parada), diferente do <see cref="SampleAsync" /> que só cobre containers vivos.
    ///     Cacheado por <see cref="DiskSampleInterval" />; a varredura das entradas expiradas roda fora do
    ///     contexto do chamador (<see cref="Task.Run(Action)" />) para não bloquear o circuito Blazor.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, long>> SampleDiskAsync(
        IEnumerable<Guid> instanceIds, CancellationToken ct = default)
    {
        var ids = instanceIds.ToList();
        var now = DateTime.UtcNow;

        // Só recalcula o que expirou (ou nunca foi medido); o resto sai do cache instantaneamente
        var stale = ids
            .Where(id => !_diskCache.TryGetValue(id, out var e) || now - e.At >= DiskSampleInterval)
            .ToList();

        if (stale.Count > 0)
            await Task.Run(() =>
            {
                foreach (var id in stale)
                {
                    if (ct.IsCancellationRequested) break;
                    var bytes = DirectorySizeBytes(Path.Combine(_serversRoot, id.ToString()));
                    _diskCache[id] = (DateTime.UtcNow, bytes);
                }
            }, ct);

        var result = new Dictionary<Guid, long>(ids.Count);
        foreach (var id in ids)
            if (_diskCache.TryGetValue(id, out var e))
                result[id] = e.Bytes;

        return result;
    }

    private async Task<ServerInstanceStats?> SampleOneAsync(ContainerListResponse container, CancellationToken ct)
    {
        if (ParseInstanceId(container) is not { } instanceId) return null;

        try
        {
            // IProgress síncrono: o Docker.DotNet chama Report() dentro do próprio loop de leitura,
            // então o valor já está setado quando o await retorna (sem corrida com o SynchronizationContext).
            var capture = new FirstStatCapture();
            await docker.Client.Containers.GetContainerStatsAsync(
                container.ID,
                new ContainerStatsParameters { Stream = false },
                capture,
                ct);

            if (capture.Value is not { } stat) return null;

            return new ServerInstanceStats(instanceId, ComputeCpuPercent(stat), ComputeMemoryUsedBytes(stat));
        }
        catch
        {
            return null; // container sumiu/parou no meio da amostragem → fica de fora desta rodada
        }
    }

    // Nome do container é "/tcmine-mc-{guid}"; extrai o Guid da instância (null se não casar o padrão).
    private static Guid? ParseInstanceId(ContainerListResponse container)
    {
        foreach (var raw in container.Names)
        {
            var name = raw.TrimStart('/');
            if (name.StartsWith(NamePrefix, StringComparison.Ordinal)
                && Guid.TryParse(name[NamePrefix.Length..], out var id))
                return id;
        }

        return null;
    }

    // % de CPU do container (0–100, onde 100 = todos os núcleos vistos pelo container saturados). Fórmula
    // padrão do `docker stats`: fração do tempo de CPU do sistema consumida pelo container × nº de núcleos.
    private static double ComputeCpuPercent(ContainerStatsResponse stat)
    {
        var cpuDelta = (double)stat.CPUStats.CPUUsage.TotalUsage - stat.PreCPUStats.CPUUsage.TotalUsage;
        var systemDelta = (double)stat.CPUStats.SystemUsage - stat.PreCPUStats.SystemUsage;
        if (cpuDelta <= 0 || systemDelta <= 0) return 0;

        var cpus = stat.CPUStats.OnlineCPUs;
        if (cpus == 0) cpus = (uint)(stat.CPUStats.CPUUsage.PercpuUsage?.Count ?? Environment.ProcessorCount);

        return Math.Clamp(cpuDelta / systemDelta * cpus * 100d, 0, 100);
    }

    // Memória "real" do container: uso total menos o page cache (reclamável), como o `docker stats` mostra.
    // O campo do cache varia entre cgroup v2 ("inactive_file") e v1 ("cache").
    private static long ComputeMemoryUsedBytes(ContainerStatsResponse stat)
    {
        var used = (long)stat.MemoryStats.Usage;

        if (stat.MemoryStats.Stats is { } s)
        {
            if (s.TryGetValue("inactive_file", out var inactive)) used -= (long)inactive;
            else if (s.TryGetValue("cache", out var cache)) used -= (long)cache;
        }

        return Math.Max(0, used);
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
            try
            {
                entries = dir.GetFileSystemInfos();
            }
            catch
            {
                continue;
            } // sem permissão / pasta removida durante a varredura

            foreach (var entry in entries)
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) continue; // ignora links

                if (entry is DirectoryInfo sub) stack.Push(sub);
                else if (entry is FileInfo file) total += file.Length;
            }
        }

        return total;
    }

    // Captura só a primeira amostra reportada. Report() é chamado sincronamente pelo leitor do stream.
    private sealed class FirstStatCapture : IProgress<ContainerStatsResponse>
    {
        public ContainerStatsResponse? Value { get; private set; }

        public void Report(ContainerStatsResponse value)
        {
            Value ??= value;
        }
    }
}

/// <summary>Métricas de runtime de uma instância (container em execução) num instante.</summary>
public readonly record struct ServerInstanceStats(Guid InstanceId, double CpuPercent, long MemoryUsedBytes)
{
    public double MemoryUsedMb => MemoryUsedBytes / 1024d / 1024d;
    public double MemoryUsedGb => MemoryUsedBytes / 1024d / 1024d / 1024d;
}