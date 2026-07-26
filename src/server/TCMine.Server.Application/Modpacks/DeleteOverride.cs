using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

public sealed class DeleteOverride(IModpackRepository repository)
{
    public async Task<Result> HandleAsync(Guid versionId, string path, CancellationToken ct)
    {
        var version = await repository.GetVersionAsync(versionId, ct);
        if (version is null)
            return Result.Fail("Versão não encontrada.");

        if (version.State is not ModpackVersionState.Draft)
            return Result.Fail("Só é possível editar overrides em rascunho.");

        var file = version.Files.FirstOrDefault(f =>
            f.Origin == ModFileOrigin.Override
            && f.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (file is null)
            return Result.Fail("Arquivo não encontrado.");

        await repository.RemoveFileAsync(version.Id, file.Id, ct);
        return Result.Success();
    }
}