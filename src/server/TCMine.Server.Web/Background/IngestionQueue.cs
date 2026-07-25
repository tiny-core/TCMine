using System.Threading.Channels;
using TCMine.Server.Application.Modpacks;

namespace TCMine.Server.Web.Background;

/// <summary>
///     Fila de trabalhos de ingestão.
///     Channel é a estrutura certa aqui: produtor (o request do admin) e
///     consumidor (o serviço de fundo) desacoplados, com espera assíncrona sem
///     consumir thread. Unbounded porque o volume é baixo — um admin não enfileira
///     milhares de ingestões por segundo.
/// </summary>
public sealed class IngestionQueue
{
    private readonly Channel<IngestionJob> _channel =
        Channel.CreateUnbounded<IngestionJob>();

    public ValueTask EnqueueAsync(IngestionJob job, CancellationToken ct)
    {
        return _channel.Writer.WriteAsync(job, ct);
    }

    public IAsyncEnumerable<IngestionJob> ReadAllAsync(CancellationToken ct)
    {
        return _channel.Reader.ReadAllAsync(ct);
    }
}

public sealed record IngestionJob(Guid VersionId, IReadOnlyList<ModIngestionItem> Items);