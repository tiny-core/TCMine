using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using TCMine_Application.Contracts;
using TCMine_Application.Modpack;
using TCMine_Domain.Entities;
using TCMine_Domain.Modpack;
using TCMine_Server.Infrastructure.CurseForge;
using TCMine_Server.Infrastructure.FileSystem;
using TCMine_Server.Infrastructure.Persistence;
using TCMine_Server.Infrastructure.Server;

namespace TCMine_Server.Infrastructure.Minecraft;

/// <summary>
///     Edição e import de modpacks no servidor. O fluxo principal é <b>manual</b>: o admin cria o
///     modpack, pesquisa mods por nome e adiciona; o import de um modpack inteiro do CurseForge é uma
///     opção que mescla no rascunho. Em todos os casos o servidor é a fonte dos jars — o cache/hash de
///     cada arquivo vive no <see cref="ModFileCacheService" />, e o launcher depois baixa do servidor.
///     Segue a política de escrita-só-ao-Guardar: a página edita um rascunho destacado em memória e
///     só <see cref="SaveAsync" /> persiste no banco. Os jars, porém, são baixados ao adicionar cada
///     mod (operação cara isolada por item), para o Guardar ficar leve.
///     Preocupações vizinhas foram extraídas para não acumular responsabilidades aqui:
///     <list type="bullet">
///         <item><see cref="ModFileCacheService" /> — cache de jars, SHA-1, marcação de órfãos, upload.</item>
///         <item><see cref="ModpackUpdateService" /> — checagem de atualizações no CurseForge.</item>
///         <item><see cref="ModpackOverridesService" /> — edição interativa de overrides + histórico.</item>
///     </list>
///     Aqui fica só a extração do bundle inicial de overrides no import (<see cref="ExtractOverrides" />).
/// </summary>
public sealed class ModpackImportService(
    AppDbContext db,
    CurseForgeApiClient cf,
    ModFileCacheService cache,
    ContentNotifier notifier,
    IHostEnvironment env)
{
    private readonly string _root = env.ContentRootPath;

    // ── Busca no CurseForge (delegada ao client; a página só conhece este serviço) ─────────────

    public Task<bool> IsCfConfiguredAsync(CancellationToken ct = default)
    {
        return cf.IsConfiguredAsync(ct);
    }

    public Task<List<CfSearchResultDto>> SearchModsAsync(
        string query, string? gameVersion, CancellationToken ct = default)
    {
        return cf.SearchModsAsync(query, gameVersion, ct);
    }

    public Task<List<CfSearchResultDto>> SearchModpacksAsync(string query, CancellationToken ct = default)
    {
        return cf.SearchModpacksAsync(query, ct);
    }

    // ── Adicionar mod da busca (resolve o arquivo + baixa o jar) ───────────────────────────────

    /// <summary>
    ///     Resolve o arquivo mais recente de um mod (para a versão MC + loader do modpack), baixa o jar
    ///     e devolve uma entidade <b>destacada</b> pronta para entrar no rascunho. Não toca no banco —
    ///     a gravação acontece só no Guardar.
    /// </summary>
    public async Task<List<ModAddResultDto>> AddFromSearchAsync(
        long modId, string? gameVersion, ModLoader loader, ModSide side = ModSide.Both,
        CancellationToken ct = default)
    {
        // Resolve o mod pedido e, em largura, as suas dependências OBRIGATÓRIAS (transitivo). O visited
        // evita ciclos e duplicatas. O primeiro resultado é o mod pedido; os demais são dependências.
        var results = new List<ModAddResultDto>();
        var visited = new HashSet<long> { modId };
        var queue = new Queue<long>();
        queue.Enqueue(modId);

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            var resolved = await ResolveModAsync(currentId, gameVersion, loader, side, ct);
            if (resolved is null) continue; // mod sem arquivo utilizável → ignora

            results.Add(resolved.Value.Result);
            foreach (var depId in resolved.Value.RequiredDeps)
                if (visited.Add(depId))
                    queue.Enqueue(depId); // só enfileira deps inéditas
        }

        return results;
    }

    // Resolve um mod num arquivo (filtrado por MC+loader, com fallback) + flag de compatibilidade + as
    // dependências obrigatórias dele. Devolve null se o mod não tem nenhum arquivo utilizável.
    private async Task<(ModAddResultDto Result, IReadOnlyList<long> RequiredDeps)?> ResolveModAsync(
        long modId, string? gameVersion, ModLoader loader, ModSide side, CancellationToken ct)
    {
        var compatibleFiles =
            await cf.GetModFilesAsync(modId, gameVersion, CurseForgeApiClient.ModLoaderType(loader), ct);
        var compatible = compatibleFiles.Count > 0;

        var files = compatible ? compatibleFiles : await cf.GetModFilesAsync(modId, null, null, ct);
        var file = files.FirstOrDefault();
        if (file is null) return null;

        var info = await cf.GetModsAsync([modId], ct);
        info.TryGetValue(modId, out var modRef);

        var entry = new ModEntryEntity
        {
            CurseModId = modId,
            FileId = file.Id,
            Name = modRef?.Name ?? file.FileName,
            Version = file.DisplayName,
            FileName = file.FileName,
            DownloadUrl = CurseForgeImporter.ResolveDownloadUrl(file) ?? string.Empty,
            Target = modRef is null ? "mod" : CurseForgeImporter.ClassToTarget(modRef.ClassId),
            Side = side
        };

        return (new ModAddResultDto(entry, compatible), file.RequiredDependencyModIds ?? []);
    }

    /// <summary>
    ///     Lista os arquivos (versões) de um mod do CurseForge para o seletor de versão — filtrados por MC +
    ///     loader; se não houver compatível, devolve todos (e o seletor mostra o aviso). Busca <b>lazy</b>:
    ///     chamada só quando o admin pede para trocar a versão, não na listagem.
    /// </summary>
    public async Task<List<CfFileRefDto>> ListModVersionsAsync(
        long modId, string? gameVersion, ModLoader loader, CancellationToken ct = default)
    {
        var compatible = await cf.GetModFilesAsync(modId, gameVersion, CurseForgeApiClient.ModLoaderType(loader), ct);
        return compatible.Count > 0 ? compatible : await cf.GetModFilesAsync(modId, null, null, ct);
    }

    // ── Import de modpack inteiro (opcional; mescla no rascunho) ───────────────────────────────

    /// <summary>
    ///     Importa um modpack do CurseForge num resultado destacado (metadados + mods já baixados, com
    ///     <c>Side</c> inferido pelo server pack, + bundle de overrides). A página mescla no rascunho e
    ///     só grava no Guardar.
    /// </summary>
    public async Task<DraftImportDto<ModEntryEntity>> ImportModpackToDraftAsync(long projectId,
        IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var imported = await CurseForgeImporter.ImportAsync(projectId, cf, progress, ct)
                       ?? throw new InvalidOperationException(
                           "Não foi possível importar (sem manifesto válido).");

        progress?.Report("Baixando o server pack e inferindo o lado dos mods…");
        var serverPackFiles = await cache.LoadServerPackFileNamesAsync(imported.ServerPackFileId, ct);

        progress?.Report($"Mesclando {imported.Mods.Count} mods no rascunho…");

        // Monta as entidades SEM baixar os jars — o download é deferido para o Guardar (com progresso)
        var mods = imported.Mods.Select(mod => new ModEntryEntity
        {
            CurseModId = mod.ModId, FileId = mod.FileId, Name = mod.Name, Version = mod.Version,
            FileName = mod.FileName, DownloadUrl = mod.DownloadUrl, Target = mod.Target,
            Side = CurseForgeImporter.InferSide(mod.FileName, serverPackFiles)
        }).ToList();

        // Metadados do projeto no CF (descrição + link da página) — best-effort; não parte o import.
        CfProjectInfoDto? projectInfo = null;
        try
        {
            projectInfo = await cf.GetProjectInfoAsync(projectId, ct);
        }
        catch
        {
            /* segue sem descrição/link */
        }

        return new DraftImportDto<ModEntryEntity>(
            imported.Name, imported.Version, imported.Minecraft,
            imported.Loader, imported.LoaderVersion, mods, imported.Overrides,
            imported.CurseProjectId, imported.CurseFileId,
            projectInfo?.Summary, projectInfo?.WebsiteUrl);
    }

    // ── Leitura/gestão ─────────────────────────────────────────────────────────────────────────

    /// <summary>Linhas de modpack para a tabela do painel (projeção leve, sem rastreamento).</summary>
    public async Task<List<ModpackAdminRowDto>> ListAsync(CancellationToken ct = default)
    {
        return await db.Modpacks
            .AsNoTracking()
            .OrderBy(m => m.Name)
            .Select(m => new ModpackAdminRowDto(
                m.Id, m.Name, m.Version, m.Minecraft, m.Loader, m.LoaderVersion,
                m.Mods.Count, m.Servers.Count, m.IsPublished, m.HasOverrides, m.UpdatedAt, m.CurseForgeUrl))
            .ToListAsync(ct);
    }

    /// <summary>Carrega o modpack destacado (vínculos de mod + arquivo + servidores) para edição, ou null.</summary>
    public async Task<ModpackEntity?> GetForEditAsync(Guid uid, CancellationToken ct = default)
    {
        return await db.Modpacks
            .AsNoTracking()
            .Include(m => m.Mods).ThenInclude(mm => mm.ModFile)
            .Include(m => m.Servers)
            .FirstOrDefaultAsync(m => m.Id == uid, ct);
    }

    /// <summary>
    ///     Achata os vínculos de um modpack carregado em <see cref="ModEntryEntity" /> (modelo do editor),
    ///     juntando os campos do <see cref="ModFileEntity" /> com os atributos por-modpack. Respeita a ordem.
    /// </summary>
    public static List<ModEntryEntity> FlattenMods(ModpackEntity pack)
    {
        return pack.Mods
            .Where(mm => mm.ModFile is not null)
            .OrderBy(mm => mm.SortOrder)
            .Select(mm => new ModEntryEntity
            {
                CurseModId = mm.ModFile!.CurseModId, FileId = mm.FileId, Name = mm.ModFile.Name,
                Version = mm.ModFile.Version, FileName = mm.ModFile.FileName,
                DownloadUrl = mm.ModFile.DownloadUrl, Sha1 = mm.ModFile.Sha1,
                FileLength = mm.ModFile.FileLength, Target = mm.Target, Side = mm.Side
            })
            .ToList();
    }

    /// <summary>Apaga o modpack (cascata em mods/servidores) e os overrides extraídos do slug.</summary>
    public async Task DeleteAsync(Guid uid, CancellationToken ct = default)
    {
        var pack = await db.Modpacks.FirstOrDefaultAsync(m => m.Id == uid, ct);
        if (pack is null) return;

        // Guarda de negócio: instâncias de servidor têm FK Restrict para o modpack (ver AppDbContext).
        // Sem esta checagem, o SaveChanges falharia com um DbUpdateException genérico — aqui damos uma
        // mensagem clara e acionável, nomeando os servidores que impedem a remoção.
        var blockingServers = await db.ServerInstances
            .Where(s => s.ModpackId == uid)
            .Select(s => s.Name)
            .ToListAsync(ct);

        if (blockingServers.Count > 0)
        {
            var names = string.Join(", ", blockingServers);
            throw new InvalidOperationException(
                $"O modpack \"{pack.Name}\" tem {blockingServers.Count} servidor(es) atrelado(s) ({names}). " +
                "Apague esse(s) servidor(es) na página de edição do modpack antes de remover.");
        }

        // Arquivos vinculados a este modpack — candidatos a órfão após a remoção (cascade)
        var affectedFileIds = await db.ModpackMods
            .Where(l => l.ModpackId == uid)
            .Select(l => l.FileId)
            .ToListAsync(ct);

        db.Modpacks.Remove(pack);

        // Histórico de overrides não tem FK para o modpack (chaveado só pelo slug) — limpa manualmente
        var history = await db.OverrideHistory
            .Where(h => h.ModpackId == uid)
            .ToListAsync(ct);

        db.OverrideHistory.RemoveRange(history);

        await db.SaveChangesAsync(ct);

        // Marca como órfãos os arquivos que ficaram sem nenhum modpack após a remoção
        await cache.MarkOrphansAsync(affectedFileIds, ct);

        // Jars ficam no cache (compartilhado entre modpacks); só os overrides do slug são removidos
        var dir = Path.Combine(ServerPaths.Modpacks(_root), uid.ToString());
        if (Directory.Exists(dir)) Directory.Delete(dir, true);

        // Avisa os launchers ligados (SSE) que o catálogo mudou
        notifier.Bump();
    }

    // ── Persistência (escrita-só-ao-Guardar) ───────────────────────────────────────────────────

    /// <summary>
    ///     Salva <b>só os metadados</b> do modpack (identidade, versões, extras) — para o modal de Detalhes do
    ///     hub, sem mexer em mods/servidores/overrides. Bumpa <c>UpdatedAt</c> (marca instâncias derivadas
    ///     como desatualizadas) e avisa os launchers. Lança se o modpack ainda não existe.
    /// </summary>
    public async Task UpdateMetadataAsync(ModpackEntity draft, CancellationToken ct = default)
    {
        var tracked = await db.Modpacks.FirstOrDefaultAsync(m => m.Id == draft.Id, ct)
                      ?? throw new InvalidOperationException("Modpack não encontrado.");

        tracked.Name = draft.Name;
        tracked.Version = draft.Version;
        tracked.Minecraft = draft.Minecraft;
        tracked.Loader = draft.Loader;
        tracked.LoaderVersion = draft.LoaderVersion;
        tracked.Description = draft.Description;
        tracked.CurseForgeUrl = draft.CurseForgeUrl;
        tracked.IsPublished = draft.IsPublished;
        tracked.RecommendedRamMb = draft.RecommendedRamMb;
        tracked.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        notifier.Bump();
    }

    // ── Conexões divulgadas manuais (modal do hub) ────────────────────────────────────────────────

    /// <summary>
    ///     Conexões <b>manuais</b> divulgadas pelo modpack (servidores externos cadastrados à mão). Exclui
    ///     as auto-geradas por instâncias (<c>ServerInstanceId != null</c>), que são geridas pela instância.
    ///     Devolve cópias destacadas para o painel editar livremente.
    /// </summary>
    public async Task<List<ServerEntryEntity>> GetManualConnectionsAsync(Guid modpackId, CancellationToken ct = default)
    {
        return await db.Servers.AsNoTracking()
            .Where(s => s.ModpackId == modpackId && s.ServerInstanceId == null)
            .OrderBy(s => s.Name)
            .Select(s => new ServerEntryEntity
                { Name = s.Name, Address = s.Address, Port = s.Port, ModpackId = modpackId })
            .ToListAsync(ct);
    }

    /// <summary>
    ///     Substitui as conexões manuais do modpack pelo conjunto editado (as auto-geradas por instâncias
    ///     ficam intactas). Avisa os launchers (SSE).
    /// </summary>
    public async Task SaveConnectionsAsync(
        Guid modpackId, IReadOnlyList<ServerEntryEntity> entries, CancellationToken ct = default)
    {
        var existing = await db.Servers
            .Where(s => s.ModpackId == modpackId && s.ServerInstanceId == null)
            .ToListAsync(ct);
        db.Servers.RemoveRange(existing);

        foreach (var e in entries)
            db.Servers.Add(new ServerEntryEntity
            {
                Name = e.Name, Address = e.Address, Port = e.Port,
                ModpackId = modpackId, ServerInstanceId = null
            });

        await db.SaveChangesAsync(ct);
        notifier.Bump();
    }

    /// <summary>
    ///     Persiste o rascunho: cria/atualiza o modpack, reconcilia servidores e os vínculos de mod
    ///     (o formulário é a fonte da verdade), faz upsert dos arquivos compartilhados
    ///     (<see cref="ModFileEntity" />), garante o cache de jars ainda sem hash e extrai overrides
    ///     pendentes. <paramref name="mods" /> é a lista plana do editor (ver <see cref="FlattenMods" />).
    /// </summary>
    public async Task SaveAsync(
        ModpackEntity draft, IReadOnlyList<ModEntryEntity> mods, byte[]? pendingOverrides = null,
        ModpackImportSourceEntity? importSource = null,
        IProgress<SaveProgressDto>? progress = null, CancellationToken ct = default)
    {
        // Dedup defensivo por FileId (a junção tem PK composta ModpackId+FileId); preserva a ordem
        var desired = mods
            .GroupBy(m => m.FileId)
            .Select(g => g.Last())
            .ToList();

        // Baixa os jars ainda sem hash (vindos do import ou da busca, que não baixam na hora).
        // Reporta o progresso para a UI antes de cada download.
        var pending = desired
            .Where(m => string.IsNullOrWhiteSpace(m.Sha1) && !string.IsNullOrWhiteSpace(m.DownloadUrl))
            .ToList();

        for (var i = 0; i < pending.Count; i++)
        {
            var mod = pending[i];
            progress?.Report(new SaveProgressDto(i + 1, pending.Count, mod.Name));
            var (sha1, length) = await cache.EnsureCachedAsync(mod.FileId, mod.FileName, mod.DownloadUrl, ct);
            mod.Sha1 = sha1;
            mod.FileLength = length;
        }

        progress?.Report(new SaveProgressDto(pending.Count, pending.Count, "Gravando…"));

        var tracked = await db.Modpacks
            .Include(m => m.Mods)
            .Include(m => m.Servers)
            .FirstOrDefaultAsync(m => m.Id == draft.Id, ct);

        if (tracked is null)
        {
            tracked = new ModpackEntity { Id = draft.Id };
            db.Modpacks.Add(tracked);
        }

        tracked.Name = draft.Name;
        tracked.Version = draft.Version;
        tracked.Minecraft = draft.Minecraft;
        tracked.Loader = draft.Loader;
        tracked.LoaderVersion = draft.LoaderVersion;
        tracked.Description = draft.Description;
        tracked.CurseForgeUrl = draft.CurseForgeUrl;
        tracked.IsPublished = draft.IsPublished;
        tracked.RecommendedRamMb = draft.RecommendedRamMb;
        tracked.UpdatedAt = DateTime.UtcNow; // marca a alteração para sync incremental do launcher

        // Upsert dos arquivos compartilhados (um por FileId, reusados entre modpacks)
        await UpsertModFilesAsync(desired, ct);

        // Reconcilia os vínculos do modpack in-place (evita delete+insert da mesma PK composta):
        // atualiza os que ficam, remove os que saíram, adiciona os novos — com a ordem do formulário.
        var desiredIds = desired.Select(m => m.FileId).ToHashSet();
        // Arquivos que saíram deste modpack — candidatos a virar órfãos (se nenhum outro pack usar)
        var removedIds = tracked.Mods.Select(l => l.FileId).Where(id => !desiredIds.Contains(id)).ToList();
        foreach (var link in tracked.Mods.Where(l => !desiredIds.Contains(l.FileId)).ToList())
            db.ModpackMods.Remove(link);

        var current = tracked.Mods.ToDictionary(l => l.FileId);
        for (var i = 0; i < desired.Count; i++)
        {
            var m = desired[i];
            if (current.TryGetValue(m.FileId, out var link))
            {
                link.Side = m.Side;
                link.Target = m.Target;
                link.SortOrder = i;
            }
            else
            {
                tracked.Mods.Add(new ModpackModEntity
                {
                    ModpackId = tracked.Id, FileId = m.FileId,
                    Side = m.Side, Target = m.Target, SortOrder = i
                });
            }
        }

        // Servidores: substitui pelo estado do formulário (entidades destacadas, sem chave própria)
        db.Servers.RemoveRange(tracked.Servers);
        tracked.Servers = draft.Servers.Select(x => new ServerEntryEntity
        {
            Name = x.Name, Address = x.Address, Port = x.Port
        }).ToList();

        // Overrides: bundle novo (do import) substitui; sem bundle, preserva o estado atual
        tracked.HasOverrides = pendingOverrides is not null
            ? ExtractOverrides(draft.Id, pendingOverrides)
            : draft.HasOverrides;

        // Origem do import (quando veio de um import nesta sessão): registra/atualiza a versão aplicada
        if (importSource is not null)
            await UpsertImportSourceAsync(tracked.Id, importSource, ct);

        await db.SaveChangesAsync(ct);

        // Atualiza o marcador de órfão dos arquivos que saíram (agora que os vínculos estão gravados)
        await cache.MarkOrphansAsync(removedIds, ct);

        // Avisa os launchers ligados (SSE) que o catálogo mudou
        notifier.Bump();
    }

    // Cria ou atualiza os ModFile dos mods desejados (identidade = FileId). A última gravação vence
    // nos metadados — o jar em si é imutável por FileId, então isto só atualiza nome/versão/hash.
    private async Task UpsertModFilesAsync(IReadOnlyList<ModEntryEntity> desired, CancellationToken ct)
    {
        var ids = desired.Select(m => m.FileId).ToList();
        var existing = await db.ModFiles
            .Where(f => ids.Contains(f.FileId))
            .ToDictionaryAsync(f => f.FileId, ct);

        foreach (var m in desired)
            if (existing.TryGetValue(m.FileId, out var file))
            {
                file.CurseModId = m.CurseModId;
                file.Name = m.Name;
                file.Version = m.Version;
                file.FileName = m.FileName;
                file.DownloadUrl = m.DownloadUrl;
                file.Sha1 = m.Sha1;
                file.FileLength = m.FileLength;
                file.OrphanedAt = null; // está sendo vinculado a este modpack — não é órfão
            }
            else
            {
                var created = new ModFileEntity
                {
                    FileId = m.FileId, CurseModId = m.CurseModId, Name = m.Name, Version = m.Version,
                    FileName = m.FileName, DownloadUrl = m.DownloadUrl, Sha1 = m.Sha1,
                    FileLength = m.FileLength
                };
                db.ModFiles.Add(created);
                existing[m.FileId] = created; // evita duplicar se houver repetição na lista
            }
    }

    // Cria/atualiza a linha de origem do import. Um novo import limpa o cache de "latest" (acabou de
    // ficar em dia). Não chama SaveChanges — faz parte da transação do SaveAsync.
    private async Task UpsertImportSourceAsync(
        Guid modpackId, ModpackImportSourceEntity source, CancellationToken ct)
    {
        var existing = await db.ModpackImportSources.FirstOrDefaultAsync(s => s.ModpackId == modpackId, ct);
        if (existing is null)
        {
            source.ModpackId = modpackId;
            source.ImportedAt = DateTime.UtcNow;
            db.ModpackImportSources.Add(source);
            return;
        }

        existing.CurseProjectId = source.CurseProjectId;
        existing.CurseProjectName = source.CurseProjectName;
        existing.InstalledFileId = source.InstalledFileId;
        existing.InstalledVersion = source.InstalledVersion;
        existing.ImportedAt = DateTime.UtcNow;
        // Acabou de importar → está em dia; zera o cache de checagem
        existing.LastCheckedAt = null;
        existing.LatestFileId = null;
        existing.LatestVersion = null;
    }

    /// <summary>
    ///     Extrai o bundle de overrides para <c>data-server/modpacks/{slug}/overrides/</c>, substituindo
    ///     o conteúdo anterior. Devolve <c>true</c> se havia overrides. Editável depois pelo painel
    ///     (ver <see cref="ModpackOverridesService" />).
    /// </summary>
    private bool ExtractOverrides(Guid uid, byte[]? overrides)
    {
        var target = Path.Combine(ServerPaths.Modpacks(_root), uid.ToString(), "overrides");

        // Limpa a versão anterior para o conteúdo refletir exatamente o último import
        if (Directory.Exists(target)) Directory.Delete(target, true);

        if (overrides is null || overrides.Length == 0) return false;

        Directory.CreateDirectory(target);
        using var ms = new MemoryStream(overrides);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        // ExtractToDirectory protege contra zip-slip (caminhos fora do destino) a partir do .NET
        zip.ExtractToDirectory(target, true);
        return true;
    }
}