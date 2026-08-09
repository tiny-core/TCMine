using System.Text.RegularExpressions;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Importa um modpack de uma origem externa (CurseForge) como um Modpack
///     novo aqui: cria o container e a primeira versão em rascunho, grava os
///     overrides e enfileira o download dos mods.
///     A versão nasce em Draft de propósito — publicar continua sendo decisão
///     explícita do admin, mesmo para pack importado.
/// </summary>
public sealed partial class ImportUpstreamPack(
    IEnumerable<IUpstreamPackSource> sources,
    IModpackRepository repository,
    IBlobStore blobStore,
    IIngestionQueue ingestionQueue,
    IJobProgressReporter progress,
    ICurrentUserScope scope)
{
    /// <summary>
    ///     <paramref name="jobId" /> identifica a importação para o acompanhamento
    ///     antes de existir uma versão: a UI já mostra "baixando o pack" enquanto
    ///     o modpack nem foi criado.
    /// </summary>
    public async Task<Result<Guid>> HandleAsync(
        ModFileOrigin origin, string projectId, string? fileId, CancellationToken ct,
        Guid jobId = default, string? displayName = null)
    {
        var title = displayName is { Length: > 0 } ? $"Importando {displayName}" : "Importando pack";
        void Step(string step, int done, int total)
        {
            if (jobId != default)
                progress.Report(jobId, new JobProgress(title, step, done, total));
        }

        Step("Consultando a origem…", 0, 0);

        IUpstreamPackSource? source = null;
        foreach (var candidate in sources.Where(s => s.Origin == origin))
        {
            if (!await candidate.IsAvailableAsync(ct))
                continue;

            source = candidate;
            break;
        }

        if (source is null)
        {
            progress.Complete(jobId, $"A origem {origin} não está configurada.");
            return Result<Guid>.Fail($"A origem {origin} não está configurada.");
        }

        Step("Baixando e lendo o pack…", 0, 0);

        var pack = await source.FetchAsync(projectId, fileId, ct);
        if (pack is null)
            return Result<Guid>.Fail("Não foi possível ler o pack na origem. Ele pode não permitir download por terceiros.");

        // Um pack por origem: reimportar criaria dois modpacks disputando a mesma
        // procedência, e a detecção de atualização não saberia qual atualizar.
        if (await repository.ExistsFromUpstreamAsync(origin, projectId, ct))
            return Result<Guid>.Fail("Este pack já foi importado. Use 'Verificar atualizações' na versão existente.");

        var slug = await UniqueSlugAsync(Slugify(pack.Name), ct);

        var modpack = new Modpack
        {
            OwnerId = scope.OwnerId,
            Slug = slug,
            Name = pack.Name,
            Summary = pack.Author is { Length: > 0 } author ? $"Importado do {origin}. Autor: {author}." : null,
            MinecraftVersion = pack.MinecraftVersion,
            Loader = pack.Loader,
            UpstreamProvider = origin,
            UpstreamProjectId = pack.ProjectId
        };

        await repository.CreateAsync(modpack, ct);

        var version = new ModpackVersion
        {
            ModpackId = modpack.Id,
            Version = "1.0.0",
            LoaderVersion = pack.LoaderVersion ?? "",
            UpstreamFileId = pack.FileId,
            UpstreamVersionLabel = pack.VersionLabel
        };

        // Overrides entram já resolvidos (o conteúdo veio no zip); os mods vão
        // para a fila, que baixa e hasheia em background.
        var overrideHashes = await StoreOverridesAsync(version, pack.Overrides, Step, ct);

        version.UpstreamSnapshotJson = new UpstreamSnapshot
        {
            Mods = pack.Mods.ToDictionary(m => m.ProjectId, m => m.FileId),
            Overrides = overrideHashes,
            Names = pack.Mods
                .Where(m => m.Name is { Length: > 0 })
                .ToDictionary(m => m.ProjectId, m => m.Name!)
        }.ToJson();

        await repository.AddVersionAsync(version, ct);

        // Lado sempre Both: o manifest do CurseForge NÃO diz cliente/servidor —
        // o campo 'required' dele significa "obrigatório no pack", outra coisa.
        // Tratar não-obrigatório como ClientOnly era um chute que marcava mod de
        // servidor como se fosse só de cliente.
        // 'required = false' no manifest é opcional DO PACK (o jogador escolhe),
        // não um lado. O lado vem depois, na resolução: o manifest não o traz,
        // mas o arquivo no CurseForge carrega as tags Client/Server.
        var items = pack.Mods
            .Select(m => new ModIngestionItem(origin, m.ProjectId, m.FileId, FileSide.Both, !m.Required))
            .ToList();

        if (items.Count > 0)
            await ingestionQueue.EnqueueAsync(version.Id, items, ct);

        // O acompanhamento passa daqui para a ingestão, que reporta pela versão.
        progress.Complete(jobId);

        return Result<Guid>.Success(modpack.Id);
    }

    private async Task<Dictionary<string, string>> StoreOverridesAsync(
        ModpackVersion version,
        IReadOnlyList<UpstreamPackOverride> overrides,
        Action<string, int, int> step,
        CancellationToken ct)
    {
        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var done = 0;

        foreach (var item in overrides)
        {
            // Milhares de arquivos: sem contador o admin fica minutos sem saber
            // se está andando.
            step("Gravando configs e scripts", done++, overrides.Count);
            using var content = new MemoryStream(item.Content);
            var sha = await blobStore.PutAsync(content, null, "application/octet-stream", ct);

            version.UpsertFile(new ModpackFile
            {
                ModpackVersionId = version.Id,
                Path = item.Path,
                Sha256 = sha,
                SizeBytes = item.Content.Length,
                Side = FileSide.Both,
                Origin = ModFileOrigin.Override,
                ProjectSlug = $"override:{item.Path}"
            });

            hashes[item.Path] = sha;
        }

        return hashes;
    }

    /// <summary>Acrescenta sufixo numérico enquanto o slug estiver tomado.</summary>
    private async Task<string> UniqueSlugAsync(string baseSlug, CancellationToken ct)
    {
        var candidate = string.IsNullOrWhiteSpace(baseSlug) ? "modpack" : baseSlug;
        var suffix = 2;

        while (await repository.SlugExistsAsync(candidate, ct))
            candidate = $"{baseSlug}-{suffix++}";

        return candidate;
    }

    private static string Slugify(string text)
    {
        var lowered = new string(text.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray());

        // Colapsa hifens repetidos ("A  B" viraria "a--b").
        return CollapseHyphens().Replace(lowered, "-").Trim('-');
    }

    [GeneratedRegex("-{2,}")]
    private static partial Regex CollapseHyphens();
}
