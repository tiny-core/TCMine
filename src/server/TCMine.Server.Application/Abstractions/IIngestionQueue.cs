using TCMine.Server.Application.Modpacks;

namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Avisa o worker de ingestão que uma versão tem pedidos esperando.
///     A Application define a porta; a implementação (o Channel) vive no Web. É o
///     que permite a tela disparar a ingestão sem esperar os minutos que ela leva.
///     Leva só a versão (fase 6.5): o pedido em si já está gravado como pendência
///     <c>Queued</c> pelo <see cref="IngestionScheduler" />, e o worker o lê de lá.
///     Assim a fila pode ter teto e juntar pedidos repetidos sem perder nada.
/// </summary>
public interface IIngestionQueue
{
    ValueTask EnqueueAsync(Guid versionId, CancellationToken ct);
}
