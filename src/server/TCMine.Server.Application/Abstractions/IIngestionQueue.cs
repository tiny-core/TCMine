using TCMine.Server.Application.Modpacks;

namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Enfileira um trabalho de ingestão para rodar em background.
///     A Application define a porta; a implementação (o Channel) vive no Web. É o
///     que permite a tela disparar a ingestão sem esperar os minutos que ela leva.
/// </summary>
public interface IIngestionQueue
{
    ValueTask EnqueueAsync(Guid versionId, IReadOnlyList<ModIngestionItem> items, CancellationToken ct);
}
