using System.Threading.Channels;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;

namespace TCMine.Server.Web.Background;

/// <summary>
///     Fila de trabalhos de ingestão sobre um Channel.
///     Channel desacopla produtor (o request do admin) e consumidor (o worker),
///     com espera assíncrona sem consumir thread. Unbounded porque o volume é
///     baixo — ninguém enfileira milhares de ingestões por segundo.
/// </summary>
public sealed class IngestionQueue : IIngestionQueue
{
    private readonly Channel<IngestionJob> _channel =
        Channel.CreateUnbounded<IngestionJob>();

    public ValueTask EnqueueAsync(
        Guid versionId,
        IReadOnlyList<ModIngestionItem> items,
        CancellationToken ct)
    {
        return _channel.Writer.WriteAsync(new IngestionJob(versionId, items), ct);
    }

    public IAsyncEnumerable<IngestionJob> ReadAllAsync(CancellationToken ct)
    {
        return _channel.Reader.ReadAllAsync(ct);
    }
}

public sealed record IngestionJob(Guid VersionId, IReadOnlyList<ModIngestionItem> Items);