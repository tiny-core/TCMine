using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Edita os metadados de uma versão em rascunho: número e RAM. Só em Draft —
///     versão publicada é imutável. Mods/overrides editam-se na grade, não aqui.
/// </summary>
public sealed class UpdateModpackVersion(IModpackRepository repository)
{
    public async Task<Result> HandleAsync(
        Guid versionId, string version, int? recommendedMemoryMb, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(version))
            return Result.Fail("Informe o número da versão.");

        var current = await repository.GetVersionAsync(versionId, ct);
        if (current is null)
            return Result.Fail("Versão não encontrada.");

        if (current.State is not ModpackVersionState.Draft)
            return Result.Fail("Só é possível editar uma versão em rascunho.");

        var versionText = version.Trim();

        // Se o número mudou, não pode colidir com outra versão do mesmo pack.
        if (!versionText.Equals(current.Version, StringComparison.OrdinalIgnoreCase))
        {
            var siblings = await repository.ListVersionsAsync(current.ModpackId, ct);
            if (siblings.Any(v => v.Id != versionId
                                  && v.Version.Equals(versionText, StringComparison.OrdinalIgnoreCase)))
                return Result.Fail($"A versão '{versionText}' já existe neste modpack.");
        }

        current.Version = versionText;
        current.RecommendedMemoryMb = recommendedMemoryMb;

        await repository.UpdateVersionAsync(current, ct);
        return Result.Success();
    }
}
