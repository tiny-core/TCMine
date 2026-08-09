using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Tenta de novo o que não veio: reenfileira SÓ o que falta.
///     Serve aos dois casos — versão que falhou (volta para rascunho) e versão em
///     rascunho com pendências que ainda podem mudar de resultado. O que já foi
///     baixado continua válido (o hash foi conferido), então rebaixar tudo seria
///     desperdício de banda e de cota de API.
///     Pendência por redistribuição negada nunca entra: é decisão do autor, e
///     insistir só gasta chamada e frustra o admin.
/// </summary>
public sealed class RetryModResolution(
    IModpackRepository repository,
    IIngestionQueue ingestionQueue)
{
    public async Task<Result<int>> HandleAsync(Guid versionId, CancellationToken ct)
    {
        var version = await repository.GetVersionAsync(versionId, ct);
        if (version is null)
            return Result<int>.Fail("Versão não encontrada.");

        var modpack = await repository.GetByIdAsync(version.ModpackId, ct);
        if (modpack is null)
            return Result<int>.Fail("Modpack não encontrado.");

        if (version.State is ModpackVersionState.Failed)
        {
            version.RetryAfterFailure();
        }
        else if (version.State is not ModpackVersionState.Draft)
        {
            return Result<int>.Fail(
                $"Só é possível tentar de novo numa versão em rascunho ou que falhou. Estado atual: {version.State}.");
        }

        var items = ToRetry(version, modpack);

        await repository.UpdateVersionAsync(version, ct);

        if (items.Count > 0)
            await ingestionQueue.EnqueueAsync(version.Id, items, ct);

        return Result<int>.Success(items.Count);
    }

    private static List<ModIngestionItem> ToRetry(ModpackVersion version, Modpack modpack)
    {
        var present = version.Files
            .Where(f => f.ProjectSlug is not null)
            .Select(f => f.ProjectSlug!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var items = new Dictionary<string, ModIngestionItem>(StringComparer.OrdinalIgnoreCase);

        // Pendências que ainda podem resolver.
        foreach (var pending in version.PendingMods
                     .Where(p => p.Reason is not PendingModReason.DistributionDenied))
        {
            items[pending.ProjectSlug] = new ModIngestionItem(
                pending.Origin, pending.ProjectSlug, pending.FileId, pending.Side);
        }

        // Numa versão importada, o snapshot diz o que "deveria existir": pega o
        // que sumiu sem nem ter virado pendência (ingestão interrompida no meio).
        var snapshot = UpstreamSnapshot.FromJson(version.UpstreamSnapshotJson);
        if (snapshot is not null && modpack.UpstreamProvider is { } origin)
        {
            foreach (var (projectId, fileId) in snapshot.Mods)
            {
                if (present.Contains(projectId) || items.ContainsKey(projectId))
                    continue;

                items[projectId] = new ModIngestionItem(origin, projectId, fileId, FileSide.Both);
            }
        }

        return [.. items.Values];
    }
}
