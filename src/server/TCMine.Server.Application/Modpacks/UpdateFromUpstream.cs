using System.Security.Cryptography;
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
///     O admin escolhe item a item o que entra (<see cref="UpstreamSelection" />).
///     Sem escolha, vale a padrão: entra o que não conflita, e nos conflitos
///     fica o lado do admin.
/// </summary>
public sealed class UpdateFromUpstream(
    IEnumerable<IUpstreamPackSource> sources,
    IModpackRepository repository,
    IBlobStore blobStore,
    IngestionScheduler scheduler,
    IJobProgressReporter progress)
{
    /// <summary>
    ///     Os dois lados de um config em conflito vão para a tela; acima disto o
    ///     arquivo não é texto de editar (o mesmo teto do editor de overrides).
    /// </summary>
    private const int MaxConflictPreviewBytes = 512 * 1024;

    /// <summary>
    ///     <paramref name="dryRun" /> calcula e devolve o plano sem gravar nada —
    ///     é o que a tela usa para mostrar "o que vai acontecer" antes de o admin
    ///     confirmar. <paramref name="selection" /> é o que ele marcou; nula = a
    ///     escolha padrão.
    /// </summary>
    public async Task<Result<UpstreamUpdateResult>> HandleAsync(
        Guid versionId, string newVersionNumber, CancellationToken ct, bool dryRun = false,
        Guid jobId = default, UpstreamSelection? selection = null)
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

        // Hash local dos overrides que vieram, para o plano enxergar mudanças de
        // config sem gravar nada (o dry run não pode escrever no blob store).
        // Mesmo algoritmo do store — é o que faz os hashes casarem.
        var theirOverrides = pack.Overrides.ToDictionary(
            o => o.Path,
            o => Convert.ToHexStringLower(SHA256.HashData(o.Content)),
            StringComparer.OrdinalIgnoreCase);

        var plan = UpstreamMerge.Plan(baseSnapshot, theirMods, current.Files, theirOverrides);

        if (dryRun)
        {
            progress.Complete(jobId);
            return Result<UpstreamUpdateResult>.Success(new UpstreamUpdateResult(plan, pack.VersionLabel, null)
            {
                TheirConflictContents = ConflictContents(pack, plan)
            });
        }

        if (plan.IsEmpty)
            return Result<UpstreamUpdateResult>.Fail("Nada mudou em relação à origem.");

        var resolution = UpstreamMerge.Resolve(plan, selection);

        // Tudo desmarcado: um rascunho idêntico ao atual só ocuparia espaço e
        // confundiria quem olha a lista de versões.
        if (resolution.IsEmpty)
        {
            return Result<UpstreamUpdateResult>.Fail(
                "Nenhuma mudança marcada. Marque ao menos um item para criar o rascunho.");
        }

        if (string.IsNullOrWhiteSpace(newVersionNumber))
            return Result<UpstreamUpdateResult>.Fail("Informe o número da nova versão.");

        var draft = await BuildDraftAsync(
            current, pack, resolution, theirOverrides, newVersionNumber.Trim(), Report, ct);

        // Só os mods que mudam vão para a fila: os demais já estão no rascunho
        // apontando para o mesmo blob, e rebaixá-los seria desperdício.
        var toIngest = resolution.ModsToIngest
            .Select(c => new ModIngestionItem(origin, c.ProjectId, c.ToFileId, FileSide.Both))
            .ToList();

        if (toIngest.Count > 0)
            await scheduler.ScheduleAsync(draft, toIngest, ct);

        // Daqui em diante quem reporta é a ingestão, pela versão nova.
        progress.Complete(jobId);

        return Result<UpstreamUpdateResult>.Success(
            new UpstreamUpdateResult(plan, pack.VersionLabel, draft.Id));
    }

    /// <summary>
    ///     O lado do autor de cada config em conflito, para a tela mostrar os
    ///     dois. Só os conflitos: um pack tem milhares de configs, e mandar todos
    ///     para o circuito do Blazor pesaria à toa.
    /// </summary>
    private static Dictionary<string, byte[]> ConflictContents(UpstreamPack pack, UpstreamMergePlan plan)
    {
        var paths = plan.Overrides.Conflicts
            .Where(c => c.Kind is not UpstreamConflictKind.ChangedHereRemovedThere)
            .Select(c => c.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return pack.Overrides
            .Where(o => paths.Contains(o.Path) && o.Content.Length <= MaxConflictPreviewBytes)
            .ToDictionary(o => o.Path, o => o.Content, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<ModpackVersion> BuildDraftAsync(
        ModpackVersion current, UpstreamPack pack, UpstreamResolution resolution,
        IReadOnlyDictionary<string, string> theirOverrides, string newVersion,
        Action<string, int, int> report, CancellationToken ct)
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

        // Mods que saem (removidos pelo autor, ou trocados pelo do autor) não
        // copiam — a ingestão traz o novo. Configs que o autor tirou também
        // não: sem isto, um pack que reorganiza a pasta config/ acumula lixo a
        // cada atualização, e o launcher instala esse lixo.
        foreach (var file in current.Files)
        {
            if (file.Origin is not ModFileOrigin.Override
                && file.ProjectSlug is { } slug && resolution.ModsToDrop.Contains(slug))
                continue;

            if (file.Origin is ModFileOrigin.Override && resolution.OverridesToDrop.Contains(file.Path))
                continue;

            draft.UpsertFile(file.CopyTo(draft.Id));
        }

        await ApplyOverridesAsync(draft, pack, resolution.OverridesToWrite, report, ct);

        // O retrato é o pack do autor INTEIRO, inclusive o que o admin recusou:
        // é o que faz a recusa valer como escolha dele na próxima atualização,
        // em vez de reaparecer como novidade.
        draft.UpstreamSnapshotJson = new UpstreamSnapshot
        {
            Mods = pack.Mods.ToDictionary(m => m.ProjectId, m => m.FileId),
            Overrides = new Dictionary<string, string>(theirOverrides, StringComparer.OrdinalIgnoreCase),
            Names = pack.Mods
                .Where(m => m.Name is { Length: > 0 })
                .ToDictionary(m => m.ProjectId, m => m.Name!)
        }.ToJson();

        await repository.AddVersionAsync(draft, ct);
        return draft;
    }

    /// <summary>
    ///     Grava o conteúdo do autor só nos configs marcados. Os demais já vieram
    ///     da versão atual na cópia, com o conteúdo daqui — inclusive os que o
    ///     admin editou, que é o trabalho mais caro de refazer.
    /// </summary>
    private async Task ApplyOverridesAsync(
        ModpackVersion draft, UpstreamPack pack, IReadOnlySet<string> toWrite,
        Action<string, int, int> report, CancellationToken ct)
    {
        var items = pack.Overrides.Where(o => toWrite.Contains(o.Path)).ToList();
        var done = 0;

        foreach (var item in items)
        {
            // Centenas de configs: sem contador, minutos de silêncio.
            report("Aplicando configs e scripts", done++, items.Count);
            using var content = new MemoryStream(item.Content);
            var sha = await blobStore.PutAsync(content, null, "application/octet-stream", ct);

            var existing = draft.Files.FirstOrDefault(f =>
                f.Origin is ModFileOrigin.Override
                && f.Path.Equals(item.Path, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
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
    }
}

/// <summary>
///     Plano calculado + rótulo da versão do autor. <paramref name="DraftId" /> é
///     nulo em simulação (dryRun).
/// </summary>
public sealed record UpstreamUpdateResult(UpstreamMergePlan Plan, string LatestLabel, Guid? DraftId)
{
    /// <summary>
    ///     Só na simulação: caminho → conteúdo do autor de cada config em
    ///     conflito (até 512 KB), para a tela mostrar os dois lados.
    /// </summary>
    public IReadOnlyDictionary<string, byte[]> TheirConflictContents { get; init; } =
        new Dictionary<string, byte[]>();
}
