using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Traz a atualização do autor para um rascunho NOVO, preservando o que o
///     admin mudou.
///     Nunca mexe na versão existente: se ela já estiver publicada é imutável, e
///     mesmo em rascunho sobrescrever seria destrutivo. O resultado é uma versão
///     nova em Draft, que o admin revisa e publica (ou descarta).
///     Conflitos NÃO são aplicados — o lado do admin fica, e a lista volta para
///     ele decidir mod a mod.
/// </summary>
public sealed class UpdateFromUpstream(
    IEnumerable<IUpstreamPackSource> sources,
    IModpackRepository repository,
    IBlobStore blobStore,
    IIngestionQueue ingestionQueue,
    IJobProgressReporter progress)
{
    /// <summary>
    ///     <paramref name="dryRun" /> calcula e devolve o plano sem gravar nada —
    ///     é o que a tela usa para mostrar "o que vai acontecer" antes de o admin
    ///     confirmar.
    /// </summary>
    public async Task<Result<UpstreamUpdateResult>> HandleAsync(
        Guid versionId, string newVersionNumber, CancellationToken ct, bool dryRun = false,
        Guid jobId = default)
    {
        void Report(string step, int done = 0, int total = 0)
        {
            if (jobId != default)
                progress.Report(jobId, new JobProgress("Atualizando a partir da origem", step, done, total));
        }

        var current = await repository.GetVersionAsync(versionId, ct);
        if (current is null)
            return Result<UpstreamUpdateResult>.Fail("Versão não encontrada.");

        var modpack = await repository.GetByIdAsync(current.ModpackId, ct);
        if (modpack?.UpstreamProvider is not { } origin || modpack.UpstreamProjectId is not { } projectId)
            return Result<UpstreamUpdateResult>.Fail("Este modpack não veio de uma origem externa.");

        var baseSnapshot = UpstreamSnapshot.FromJson(current.UpstreamSnapshotJson);
        if (baseSnapshot is null)
        {
            return Result<UpstreamUpdateResult>.Fail(
                "Esta versão não tem o retrato da origem, então não há como saber o que você mudou. "
                + "Importe o pack de novo para restabelecer a base.");
        }

        IUpstreamPackSource? source = null;
        foreach (var candidate in sources.Where(s => s.Origin == origin))
        {
            if (!await candidate.IsAvailableAsync(ct))
                continue;

            source = candidate;
            break;
        }

        if (source is null)
            return Result<UpstreamUpdateResult>.Fail($"A origem {origin} não está configurada.");

        // Baixar e ler o pack é o passo longo — um zip de centenas de MB.
        Report("Baixando o pack da origem…");

        var pack = await source.FetchAsync(projectId, null, ct);
        if (pack is null)
        {
            progress.Complete(jobId, "Não foi possível ler o pack na origem.");
            return Result<UpstreamUpdateResult>.Fail("Não foi possível ler o pack na origem.");
        }

        Report("Comparando com o que você tem…");

        var theirMods = pack.Mods.ToDictionary(m => m.ProjectId, m => m.FileId);
        var plan = UpstreamMerge.Plan(baseSnapshot, theirMods, current.Files);

        if (dryRun)
        {
            progress.Complete(jobId);
            return Result<UpstreamUpdateResult>.Success(new UpstreamUpdateResult(plan, pack.VersionLabel, null));
        }

        if (plan.IsEmpty)
            return Result<UpstreamUpdateResult>.Fail("Nada mudou em relação à origem.");

        if (string.IsNullOrWhiteSpace(newVersionNumber))
            return Result<UpstreamUpdateResult>.Fail("Informe o número da nova versão.");

        var draft = await BuildDraftAsync(current, pack, plan, newVersionNumber.Trim(), baseSnapshot, Report, ct);

        // Só os mods que mudam vão para a fila: os demais já estão no rascunho
        // apontando para o mesmo blob, e rebaixá-los seria desperdício.
        var toIngest = plan.Add.Concat(plan.Update)
            .Select(c => new ModIngestionItem(origin, c.ProjectId, c.ToFileId, FileSide.Both))
            .ToList();

        if (toIngest.Count > 0)
            await ingestionQueue.EnqueueAsync(draft.Id, toIngest, ct);

        // Daqui em diante quem reporta é a ingestão, pela versão nova.
        progress.Complete(jobId);

        return Result<UpstreamUpdateResult>.Success(
            new UpstreamUpdateResult(plan, pack.VersionLabel, draft.Id));
    }

    private async Task<ModpackVersion> BuildDraftAsync(
        ModpackVersion current, UpstreamPack pack, UpstreamMergePlan plan, string newVersion,
        UpstreamSnapshot baseSnapshot, Action<string, int, int> report, CancellationToken ct)
    {
        var draft = new ModpackVersion
        {
            ModpackId = current.ModpackId,
            Version = newVersion,
            LoaderVersion = pack.LoaderVersion ?? current.LoaderVersion,
            RecommendedMemoryMb = current.RecommendedMemoryMb,
            UpstreamFileId = pack.FileId,
            UpstreamVersionLabel = pack.VersionLabel
        };

        // Removidos pelo autor sem o admin ter mexido: não copiam.
        var dropped = plan.Remove.Select(r => r.ProjectId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Atualizados também não copiam o arquivo velho — a ingestão traz o novo.
        foreach (var updated in plan.Update)
            dropped.Add(updated.ProjectId);

        foreach (var file in current.Files)
        {
            if (file.ProjectSlug is { } slug && dropped.Contains(slug))
                continue;

            draft.UpsertFile(new ModpackFile
            {
                ModpackVersionId = draft.Id,
                ProjectSlug = file.ProjectSlug,
                Path = file.Path,
                Sha256 = file.Sha256,
                SizeBytes = file.SizeBytes,
                Side = file.Side,
                Optional = file.Optional,
                Origin = file.Origin,
                OriginReference = file.OriginReference,
                IconUrl = file.IconUrl
            });
        }

        // Overrides seguem a mesma regra de três vias dos mods.
        var overrideHashes = await ApplyOverridesAsync(draft, pack, baseSnapshot, report, ct);

        draft.UpstreamSnapshotJson = new UpstreamSnapshot
        {
            Mods = pack.Mods.ToDictionary(m => m.ProjectId, m => m.FileId),
            Overrides = overrideHashes,
            Names = pack.Mods
                .Where(m => m.Name is { Length: > 0 })
                .ToDictionary(m => m.ProjectId, m => m.Name!)
        }.ToJson();

        await repository.AddVersionAsync(draft, ct);
        return draft;
    }

    private async Task<Dictionary<string, string>> ApplyOverridesAsync(
        ModpackVersion draft, UpstreamPack pack, UpstreamSnapshot baseSnapshot,
        Action<string, int, int> report, CancellationToken ct)
    {
        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var done = 0;

        foreach (var item in pack.Overrides)
        {
            // Milhares de configs: sem contador, minutos de silêncio.
            report("Aplicando configs e scripts", done++, pack.Overrides.Count);
            using var content = new MemoryStream(item.Content);
            var sha = await blobStore.PutAsync(content, null, "application/octet-stream", ct);
            hashes[item.Path] = sha;

            var existing = draft.Files.FirstOrDefault(f =>
                f.Origin is ModFileOrigin.Override
                && f.Path.Equals(item.Path, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                // Mesmas três vias dos mods, agora por hash. Se o config aqui
                // ainda é igual ao que veio na importação, o admin não o tocou e
                // a versão do autor pode entrar. Se difere, ele editou — e config
                // customizado é o trabalho mais caro de refazer, então o dele
                // fica e a mudança do autor é descartada em silêncio.
                var baseSha = baseSnapshot.Overrides.GetValueOrDefault(item.Path);
                var intocado = baseSha is not null && existing.Sha256 == baseSha;

                if (!intocado || existing.Sha256 == sha)
                    continue;

                existing.Sha256 = sha;
                existing.SizeBytes = item.Content.Length;
                continue;
            }

            draft.UpsertFile(new ModpackFile
            {
                ModpackVersionId = draft.Id,
                Path = item.Path,
                Sha256 = sha,
                SizeBytes = item.Content.Length,
                Side = FileSide.Both,
                Origin = ModFileOrigin.Override,
                ProjectSlug = $"override:{item.Path}"
            });
        }

        return hashes;
    }
}

/// <summary>
///     Plano calculado + rótulo da versão do autor. <paramref name="DraftId" /> é
///     nulo em simulação (dryRun).
/// </summary>
public sealed record UpstreamUpdateResult(UpstreamMergePlan Plan, string LatestLabel, Guid? DraftId);
