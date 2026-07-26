using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Compara cada mod de uma versão com a última versão disponível na origem,
///     só por metadados (ResolveAsync não baixa nada). Atualizar não muta esta
///     versão — o diff alimenta a criação de uma versão nova (imutabilidade).
/// </summary>
public sealed class CheckModpackVersionUpdates(
    IModpackRepository repository,
    IEnumerable<IModResolver> resolvers)
{
    public async Task<Result<IReadOnlyList<ModUpdateInfo>>> HandleAsync(Guid versionId, CancellationToken ct)
    {
        var version = await repository.GetVersionAsync(versionId, ct);
        if (version is null)
            return Result<IReadOnlyList<ModUpdateInfo>>.Fail("Versão não encontrada.");

        var updates = new List<ModUpdateInfo>();

        foreach (var file in version.Files)
        {
            // Só mods com identidade e version id fixado (Modrinth, por ora) se
            // checam. CurseForge importado e uploads manuais ficam de fora.
            if (file.Origin != ModFileOrigin.Modrinth
                || file.ProjectSlug is null
                || file.OriginReference is null)
                continue;

            var resolver = resolvers.FirstOrDefault(r => r.Origin == file.Origin && r.IsAvailable);
            if (resolver is null)
                continue;

            var request = new ModRequest(file.ProjectSlug, null, version.MinecraftVersion, version.Loader);
            var resolution = await resolver.ResolveAsync(request, ct);

            // Só a versão mais recente compatível é diferente do que está fixado?
            // Guarda extra: se o nome do arquivo é idêntico, é a mesma release
            // (protege contra OriginReference velho/estranho — nunca mostra
            // "mesmo.jar → mesmo.jar").
            if (resolution is ModResolution.Resolved resolved
                && !string.Equals(resolved.VersionId, file.OriginReference, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(resolved.FileName, Path.GetFileName(file.Path), StringComparison.OrdinalIgnoreCase))
                updates.Add(new ModUpdateInfo(
                    file.ProjectSlug,
                    Path.GetFileName(file.Path),
                    file.OriginReference,
                    resolved.VersionId,
                    resolved.FileName,
                    file.Side,
                    file.Origin));
        }

        return Result<IReadOnlyList<ModUpdateInfo>>.Success(updates);
    }
}

/// <summary>Um mod com atualização disponível: o que está fixado e o que há de novo.</summary>
public sealed record ModUpdateInfo(
    string ProjectSlug,
    string CurrentFileName,
    string CurrentVersionId,
    string LatestVersionId,
    string LatestFileName,
    FileSide Side,
    ModFileOrigin Origin);