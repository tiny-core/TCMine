using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Web.Background;

/// <summary>
///     Fila de versões com ingestão pedida, sobre um Channel.
///     Channel desacopla produtor (o request do admin) e consumidor (o worker),
///     com espera assíncrona sem consumir thread.
///     Desde a fase 6.5 leva só o id da versão: o pedido está gravado como
///     pendência Queued, e o worker o lê de lá. Por isso a fila pode:
///     <list type="bullet">
///         <item>
///             juntar repetidos — uma versão que já está esperando não entra de
///             novo, e a passada dela pega os dois pedidos;
///         </item>
///         <item>
///             ter teto — antes era <c>CreateUnbounded</c>, que aceitava trabalho
///             sem limite e escondia sobrecarga. Com um rascunho por modpack, o
///             número de versões esperando não passa do número de modpacks, e o
///             teto abaixo só seria atingido com isso tudo pedido ao mesmo tempo.
///             Aí quem pede ESPERA a vaga (não perde o pedido, que já está no banco).
///         </item>
///     </list>
/// </summary>
public sealed class IngestionQueue : IIngestionQueue
{
    public const int Capacity = 100;

    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(
        new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });

    // Versões na fila e ainda não pegas pelo worker. Sai no momento em que o
    // worker a pega, e não quando termina: um pedido que chega DURANTE a passada
    // precisa de outra passada depois desta.
    private readonly ConcurrentDictionary<Guid, byte> _waiting = new();

    public async ValueTask EnqueueAsync(Guid versionId, CancellationToken ct)
    {
        if (!_waiting.TryAdd(versionId, 0))
            return; // já está esperando: a passada dela pega este pedido também

        try
        {
            await _channel.Writer.WriteAsync(versionId, ct);
        }
        catch
        {
            // Não entrou: tirar a marca, senão os próximos pedidos desta versão
            // achariam que ela está na fila e nunca a poriam lá.
            _waiting.TryRemove(versionId, out _);
            throw;
        }
    }

    public async IAsyncEnumerable<Guid> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var versionId in _channel.Reader.ReadAllAsync(ct))
        {
            _waiting.TryRemove(versionId, out _);
            yield return versionId;
        }
    }
}
