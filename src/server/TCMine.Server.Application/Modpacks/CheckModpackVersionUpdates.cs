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
    IEnumerable<IModResolver> resolvers,
    IJobProgressReporter progress)
{
    public async Task<Result<IReadOnlyList<ModUpdateInfo>>> HandleAsync(
        Guid versionId, CancellationToken ct, Guid jobId = default)
    {
        var version = await repository.GetVersionAsync(versionId, ct);
        if (version is null)
            return Result<IReadOnlyList<ModUpdateInfo>>.Fail("Versão não encontrada.");

        // MC e loader agora vivem no modpack (fixos para todas as versões).
        var modpack = await repository.GetByIdAsync(version.ModpackId, ct);
        if (modpack is null)
            return Result<IReadOnlyList<ModUpdateInfo>>.Fail("Modpack não encontrado.");

        var updates = new List<ModUpdateInfo>();

        // Uma consulta de metadados POR MOD: num pack importado são centenas de
        // idas à API, e a barra muda de antes fazia parecer que a janela tinha
        // morrido. Só contam os que de fato têm origem a consultar.
        var checkable = version.Files.Count(f =>
            f.Origin is ModFileOrigin.Modrinth or ModFileOrigin.CurseForge
            && f.ProjectSlug is not null);

        var done = 0;

        foreach (var file in version.Files)
        {
            // Só mods com identidade e id de arquivo fixado se checam — agora
            // Modrinth e CurseForge, já que ambos têm resolver. Uploads manuais
            // e overrides ficam de fora: não há origem a consultar.
            if (file.Origin is not (ModFileOrigin.Modrinth or ModFileOrigin.CurseForge)
                || file.ProjectSlug is null
                || file.OriginReference is null)
                continue;

            // Locais: o estado de nulidade de uma PROPRIEDADE é descartado pelo
            // compilador a cada await, e há dois no meio deste laço.
            var slug = file.ProjectSlug;
            var fixado = file.OriginReference;

            IModResolver? resolver = null;
            foreach (var candidate in resolvers.Where(r => r.Origin == file.Origin))
            {
                if (!await candidate.IsAvailableAsync(ct))
                    continue;

                resolver = candidate;
                break;
            }

            if (resolver is null)
                continue;

            var request = new ModRequest(slug, null, modpack.MinecraftVersion, modpack.Loader);

            if (jobId != default)
            {
                progress.Report(jobId, new JobProgress(
                    $"Verificando atualizações de {modpack.Name}", slug, done, checkable));
            }

            done++;

            var resolution = await resolver.ResolveAsync(request, ct);

            // Só a versão mais recente compatível é diferente do que está fixado?
            // Guarda extra: se o nome do arquivo é idêntico, é a mesma release
            // (protege contra OriginReference velho/estranho — nunca mostra
            // "mesmo.jar → mesmo.jar").
            if (resolution is ModResolution.Resolved resolved
                && !string.Equals(resolved.VersionId, fixado, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(resolved.FileName, Path.GetFileName(file.Path), StringComparison.OrdinalIgnoreCase))
            {
                updates.Add(new ModUpdateInfo(
                    slug,
                    Path.GetFileName(file.Path),
                    fixado,
                    resolved.VersionId,
                    resolved.FileName,
                    file.Side,
                    file.Origin));
            }
        }

        progress.Complete(jobId);

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
