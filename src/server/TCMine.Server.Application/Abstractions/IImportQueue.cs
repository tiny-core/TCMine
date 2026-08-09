using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Enfileira a importação de um pack externo para rodar em background.
///     Mesmo motivo da fila de ingestão: baixar o zip de um pack grande e gravar
///     milhares de overrides leva minutos, e prender o diálogo até o fim passa a
///     impressão de que o sistema travou.
/// </summary>
public interface IImportQueue
{
    /// <summary>
    ///     Devolve o id do job — é por ele que a UI acompanha o progresso antes
    ///     de existir um modpack para apontar.
    /// </summary>
    ValueTask<Guid> EnqueueAsync(
        ModFileOrigin origin, string projectId, string? fileId, string displayName, CancellationToken ct);
}
