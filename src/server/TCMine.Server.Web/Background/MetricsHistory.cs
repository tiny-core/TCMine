using System.Collections.Concurrent;

namespace TCMine.Server.Web.Background;

/// <summary>
///     Série temporal recente de consumo, em memória.
///     Deliberadamente NÃO vai ao banco: o painel mostra a última meia hora, e
///     gravar uma linha por servidor a cada 15 segundos criaria uma tabela que só
///     cresce, precisando de expurgo, índice e migração — muito custo para um
///     dado que perde valor em minutos. O preço é honesto: reiniciar o TCMine
///     zera o gráfico.
/// </summary>
public sealed class MetricsHistory
{
    /// <summary>Quantas amostras guardar por série. A 15s, 120 amostras = 30 minutos.</summary>
    public const int Capacity = 120;

    private readonly ConcurrentDictionary<Guid, Queue<MetricPoint>> _byServer = new();
    private readonly Lock _gate = new();
    private readonly Queue<HostPoint> _host = new();

    /// <summary>Disparado a cada coleta; as telas assinam e redesenham por empurrão.</summary>
    public event Action? Changed;

    public void AddServer(Guid serverId, MetricPoint point)
    {
        var series = _byServer.GetOrAdd(serverId, _ => new Queue<MetricPoint>());

        lock (_gate)
        {
            series.Enqueue(point);
            while (series.Count > Capacity)
                series.Dequeue();
        }
    }

    public void AddHost(HostPoint point)
    {
        lock (_gate)
        {
            _host.Enqueue(point);
            while (_host.Count > Capacity)
                _host.Dequeue();
        }
    }

    /// <summary>Avisa os assinantes que a rodada de coleta terminou.</summary>
    public void Publish() => Changed?.Invoke();

    public IReadOnlyList<MetricPoint> Server(Guid serverId)
    {
        if (!_byServer.TryGetValue(serverId, out var series))
            return [];

        lock (_gate)
        {
            return [.. series];
        }
    }

    public IReadOnlyList<HostPoint> Host()
    {
        lock (_gate)
        {
            return [.. _host];
        }
    }

    /// <summary>Some com a série de um servidor apagado — senão vaza para sempre.</summary>
    public void Forget(Guid serverId) => _byServer.TryRemove(serverId, out _);
}

public sealed record MetricPoint(DateTimeOffset At, double CpuPercent, long MemoryUsedBytes, long MemoryLimitBytes);

/// <summary>
///     Consumo do próprio TCMine e do disco onde vivem os blobs. Não é o consumo
///     do host inteiro: dentro de um container só se enxerga o próprio cgroup, e
///     fingir que é a máquina daria número errado.
/// </summary>
public sealed record HostPoint(
    DateTimeOffset At,
    double ProcessCpuPercent,
    long ProcessMemoryBytes,
    long DiskFreeBytes,
    long DiskTotalBytes);
