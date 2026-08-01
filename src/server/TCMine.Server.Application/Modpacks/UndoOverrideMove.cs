using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Modpacks;

public sealed class UndoOverrideMove(IModpackRepository repository, OverrideUndoService undo)
{
    public async Task<Result> HandleAsync(Guid versionId, CancellationToken ct)
    {
        var entry = undo.Pop(versionId);
        if (entry is null)
            return Result.Fail("Nada a desfazer.");

        var version = await repository.GetVersionAsync(versionId, ct);
        if (version is null)
            return Result.Fail("Versão não encontrada.");

        var file = version.Files.FirstOrDefault(f => f.Id == entry.FileId);
        if (file is null)
            return Result.Fail("Arquivo já não existe."); // apagado desde o move

        file.Path = entry.PreviousPath;
        file.ProjectSlug = $"override:{entry.PreviousPath}";

        await repository.UpdateVersionAsync(version, ct);
        return Result.Success();
    }
}