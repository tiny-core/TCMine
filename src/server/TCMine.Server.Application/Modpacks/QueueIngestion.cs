using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Valida e enfileira uma ingestão para uma versão em rascunho.
///     Retorna assim que enfileira — o trabalho pesado roda no worker. A tela
///     acompanha o progresso pelo estado da versão, que vai para Resolving e
///     depois Ready ou Failed.
/// </summary>
public sealed class QueueIngestion(
    IModpackRepository repository,
    IIngestionQueue queue)
{
    public async Task<Result> HandleAsync(QueueIngestionCommand command, CancellationToken ct)
    {
        var version = await repository.GetVersionAsync(command.VersionId, ct);

        if (version is null)
            return Result.Fail("Versão não encontrada.");

        if (version.State is not ModpackVersionState.Draft)
            return Result.Fail("Só é possível iniciar a ingestão de uma versão em rascunho.");

        if (command.Items.Count is 0)
            return Result.Fail("Informe ao menos um mod.");

        await queue.EnqueueAsync(version.Id, command.Items, ct);

        return Result.Success();
    }
}

public sealed record QueueIngestionCommand(Guid VersionId, IReadOnlyList<ModIngestionItem> Items);
