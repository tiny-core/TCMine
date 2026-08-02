using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Modpacks;

public sealed class DeleteModpackVersion(IModpackRepository repository, OverrideUndoService undo)
{
    public async Task<Result> HandleAsync(Guid versionId, CancellationToken ct)
    {
        var version = await repository.GetVersionAsync(versionId, ct);
        if (version is null)
            return Result.Fail("Versão não encontrada.");

        // Só rascunho. Publicada é imutável e pode ter servidores fixados —
        // apagá-la deixaria instâncias órfãs.
        if (version.State is not ModpackVersionState.Draft)
            return Result.Fail("Só é possível apagar uma versão em rascunho.");

        await repository.RemoveVersionAsync(versionId, ct);
        undo.Clear(versionId); // descarta o undo de overrides desta versão
        return Result.Success();
    }
}
