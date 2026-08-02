using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Remove um arquivo de uma versão em rascunho.
///     Só em Draft: versão publicada é imutável. Remove apenas o vínculo — o blob
///     segue no store, porque pode ser compartilhado com outras versões.
/// </summary>
public sealed class RemoveModpackFile(IModpackRepository repository)
{
    public async Task<Result> HandleAsync(Guid versionId, Guid fileId, CancellationToken ct)
    {
        var version = await repository.GetVersionAsync(versionId, ct);

        if (version is null)
            return Result.Fail("Versão não encontrada.");

        if (version.State is not ModpackVersionState.Draft)
            return Result.Fail("Só é possível remover arquivos de uma versão em rascunho.");

        await repository.RemoveFileAsync(versionId, fileId, ct);

        return Result.Success();
    }
}
