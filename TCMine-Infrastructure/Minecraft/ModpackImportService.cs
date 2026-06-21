using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using TCMine_Application.Contracts;
using TCMine_Application.Modpack;
using TCMine_Infrastructure.FileSystem;
using TCMine_Domain.Modpack;
using TCMine_Infrastructure.Persistence;
using TCMine_Domain.Entities;
using TCMine_Infrastructure.CurseForge;
using TCMine_Infrastructure.Server;

namespace TCMine_Infrastructure.Minecraft;

/// <summary>
/// Edição e import de modpacks no servidor. O fluxo principal é <b>manual</b>: o admin cria o
/// modpack, pesquisa mods por nome e adiciona; o import de um modpack inteiro do CurseForge é uma
/// opção que mescla no rascunho. Em todos os casos o servidor é a fonte dos jars — baixa cada
/// arquivo uma vez para o cache compartilhado (<see cref="ServerPaths.Mods" />), calcula SHA-1 e
/// tamanho, e o launcher depois baixa do servidor (nunca do CF).
///
/// Segue a política de escrita-só-ao-Guardar: a página edita um rascunho destacado em memória e
/// só <see cref="SaveAsync" /> persiste no banco. Os jars, porém, são baixados ao adicionar cada
/// mod (operação cara isolada por item), para o Guardar ficar leve.
/// </summary>
public sealed class ModpackImportService(
    AppDbContext db,
    CurseForgeApiClient cf,
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
    /// Resolve o arquivo mais recente de um mod (para a versão MC + loader do modpack), baixa o jar
    /// e devolve uma entidade <b>destacada</b> pronta para entrar no rascunho. Não toca no banco —
    /// a gravação acontece só no Guardar.
    /// </summary>
    public async Task<ModEntryEntity> AddFromSearchAsync(
        long modId, string? gameVersion, ModLoader loader, ModSide side = ModSide.Both,
        CancellationToken ct = default)
    {
        // Resolve o arquivo mais recente p/ a versão MC + loader; cai p/ o mais recente sem filtro.
        // NÃO baixa o jar aqui — o download acontece no Guardar (com progresso na tela).
        var files = await cf.GetModFilesAsync(modId, gameVersion, CurseForgeApiClient.ModLoaderType(loader), ct);
        if (files.Count == 0) files = await cf.GetModFilesAsync(modId, null, null, ct);

        var file = files.FirstOrDefault()
                   ?? throw new InvalidOperationException("Sem arquivos disponíveis para esse mod.");

        // Nome e destino (classe) do mod num único lote
        var info = await cf.GetModsAsync([modId], ct);
        info.TryGetValue(modId, out var modRef);

        return new ModEntryEntity
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
    }

    // ── Import de modpack inteiro (opcional; mescla no rascunho) ───────────────────────────────

    /// <summary>
    /// Importa um modpack do CurseForge num resultado destacado (metadados + mods já baixados, com
    /// <c>Side</c> inferido pelo server pack, + bundle de overrides). A página mescla no rascunho e
    /// só grava no Guardar.
    /// </summary>
    public async Task<DraftImportDto<ModEntryEntity>> ImportModpackToDraftAsync(long projectId,
        CancellationToken ct = default)
    {
        var imported = await CurseForgeImporter.ImportAsync(projectId, cf, ct)
                       ?? throw new InvalidOperationException(
                           "Não foi possível importar (sem manifesto válido).");

        var serverPackFiles = await LoadServerPackFileNamesAsync(imported.ServerPackFileId, ct);

        // Monta as entidades SEM baixar os jars — o download é deferido para o Guardar (com progresso)
        var mods = imported.Mods.Select(mod => new ModEntryEntity
        {
            CurseModId = mod.ModId, FileId = mod.FileId, Name = mod.Name, Version = mod.Version,
            FileName = mod.FileName, DownloadUrl = mod.DownloadUrl, Target = mod.Target,
            Side = CurseForgeImporter.InferSide(mod.FileName, serverPackFiles)
        }).ToList();

        return new DraftImportDto<ModEntryEntity>(
            imported.Name, imported.Version, imported.Minecraft,
            imported.Loader, imported.LoaderVersion, mods, imported.Overrides);
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
                m.Mods.Count, m.Servers.Count, m.IsPublished, m.HasOverrides, m.UpdatedAt))
            .ToListAsync(ct);
    }

    /// <summary>Carrega o modpack destacado (mods + servidores) para edição como rascunho, ou null.</summary>
    public async Task<ModpackEntity?> GetForEditAsync(Guid uid, CancellationToken ct = default)
    {
        return await db.Modpacks
            .AsNoTracking()
            .Include(m => m.Mods)
            .Include(m => m.Servers)
            .FirstOrDefaultAsync(m => m.Id == uid, ct);
    }

    /// <summary>Existe um modpack com este slug? (validação ao criar um novo)</summary>
    public Task<bool> ExistsAsync(Guid uid, CancellationToken ct = default)
    {
        return db.Modpacks.AnyAsync(m => m.Id == uid, ct);
    }

    /// <summary>Apaga o modpack (cascata em mods/servidores) e os overrides extraídos do slug.</summary>
    public async Task DeleteAsync(Guid uid, CancellationToken ct = default)
    {
        var pack = await db.Modpacks.FirstOrDefaultAsync(m => m.Id == uid, ct);
        if (pack is null) return;

        db.Modpacks.Remove(pack);

        // Histórico de overrides não tem FK para o modpack (chaveado só pelo slug) — limpa manualmente
        var history = await db.OverrideHistory
            .Where(h => h.ModpackId == uid)
            .ToListAsync(ct);

        db.OverrideHistory.RemoveRange(history);

        await db.SaveChangesAsync(ct);

        // Jars ficam no cache (compartilhado entre modpacks); só os overrides do slug são removidos
        var dir = Path.Combine(ServerPaths.Modpacks(_root), uid.ToString());
        if (Directory.Exists(dir)) Directory.Delete(dir, true);

        // Avisa os launchers ligados (SSE) que o catálogo mudou
        notifier.Bump();
    }

    // ── Persistência (escrita-só-ao-Guardar) ───────────────────────────────────────────────────

    /// <summary>
    /// Persiste o rascunho: cria/atualiza o modpack, reconcilia mods e servidores (o formulário é a
    /// fonte da verdade), garante o cache de jars ainda sem hash e extrai overrides pendentes.
    /// </summary>
    public async Task SaveAsync(
        ModpackEntity draft, byte[]? pendingOverrides = null,
        IProgress<SaveProgressDto>? progress = null, CancellationToken ct = default)
    {
        // Baixa os jars ainda sem hash (vindos do import ou da busca, que não baixam na hora).
        // Reporta o progresso para a UI antes de cada download.
        var pending = draft.Mods
            .Where(m => string.IsNullOrWhiteSpace(m.Sha1) && !string.IsNullOrWhiteSpace(m.DownloadUrl))
            .ToList();

        for (var i = 0; i < pending.Count; i++)
        {
            var mod = pending[i];
            progress?.Report(new SaveProgressDto(i + 1, pending.Count, mod.Name));
            var (sha1, length) = await EnsureCachedAsync(mod.FileId, mod.FileName, mod.DownloadUrl, ct);
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
        tracked.IsPublished = draft.IsPublished;
        tracked.RecommendedRamMb = draft.RecommendedRamMb;
        tracked.UpdatedAt = DateTime.UtcNow; // marca a alteração para sync incremental do launcher

        // Reconcilia as listas: substitui pelo estado do formulário (recria entidades destacadas)
        db.Mods.RemoveRange(tracked.Mods);
        db.Servers.RemoveRange(tracked.Servers);
        tracked.Mods = draft.Mods.Select(x => new ModEntryEntity
        {
            CurseModId = x.CurseModId, FileId = x.FileId, Name = x.Name, Version = x.Version,
            FileName = x.FileName, DownloadUrl = x.DownloadUrl, Target = x.Target,
            Side = x.Side, Sha1 = x.Sha1, FileLength = x.FileLength
        }).ToList();
        tracked.Servers = draft.Servers.Select(x => new ServerEntryEntity
        {
            Name = x.Name, Address = x.Address, Port = x.Port
        }).ToList();

        // Overrides: bundle novo (do import) substitui; sem bundle, preserva o estado atual
        tracked.HasOverrides = pendingOverrides is not null
            ? ExtractOverrides(draft.Id, pendingOverrides)
            : draft.HasOverrides;

        await db.SaveChangesAsync(ct);

        // Avisa os launchers ligados (SSE) que o catálogo mudou
        notifier.Bump();
    }

    // ── Cache de jars (SHA-1 + tamanho) ──────────────────────────────────────────────────────

    /// <summary>
    /// Garante que o jar está no cache (<c>data-server/mods/{fileId}/{fileName}</c>) e devolve
    /// o SHA-1 e o tamanho. Se já estiver em cache, só recalcula o hash a partir do disco.
    /// </summary>
    private async Task<(string? Sha1, long Length)> EnsureCachedAsync(
        long fileId, string fileName, string url, CancellationToken ct)
    {
        var dir = Path.Combine(ServerPaths.Mods(_root), fileId.ToString());
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);

        // URL vazia (mod sem download público) → sem cache, sem hash
        if (string.IsNullOrWhiteSpace(url) && !File.Exists(path))
            return (null, 0);

        if (!File.Exists(path))
        {
            await using var net = await cf.OpenStreamAsync(url, ct);
            await using var fs = File.Create(path);
            await net.CopyToAsync(fs, ct);
        }

        return (await ComputeSha1Async(path, ct), new FileInfo(path).Length);
    }

    /// <summary>SHA-1 (hex minúsculo) de um arquivo — para verificação de integridade no launcher.</summary>
    private static async Task<string> ComputeSha1Async(string path, CancellationToken ct)
    {
        await using var fs = File.OpenRead(path);
        var hash = await SHA1.HashDataAsync(fs, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // ── Server pack e overrides ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Baixa o server pack (se houver) e devolve o conjunto de nomes de jar contidos nele —
    /// base da inferência de <c>ModSide</c>. Devolve nulo quando não há server pack.
    /// </summary>
    private async Task<IReadOnlySet<string>?> LoadServerPackFileNamesAsync(
        long? serverPackFileId, CancellationToken ct)
    {
        if (serverPackFileId is not { } id) return null;

        var files = await cf.GetFilesAsync([id], ct);
        if (!files.TryGetValue(id, out var file)) return null;

        var url = CurseForgeImporter.ResolveDownloadUrl(file);
        if (string.IsNullOrWhiteSpace(url)) return null;

        using var buffer = new MemoryStream();
        await using (var net = await cf.OpenStreamAsync(url, ct))
        {
            await net.CopyToAsync(buffer, ct);
        }

        buffer.Position = 0;
        await using var zip = new ZipArchive(buffer, ZipArchiveMode.Read);

        // Só importa o nome base do jar — o pack guarda em mods/, o manifesto referencia o nome
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
            if (entry.FullName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
                names.Add(Path.GetFileName(entry.FullName));

        return names;
    }

    /// <summary>
    /// Extrai o bundle de overrides para <c>data-server/modpacks/{slug}/overrides/</c>, substituindo
    /// o conteúdo anterior. Devolve <c>true</c> se havia overrides. Editável depois pelo painel.
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

    // ── Upload manual de jar ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Recebe um jar enviado pelo admin, guarda-o no cache compartilhado e devolve uma entidade de mod
    /// <b>destacada</b> (pronta para entrar no rascunho), já com SHA-1/tamanho. O <c>FileId</c> é
    /// sintético e <b>negativo</b> (derivado do conteúdo) para nunca colidir com ids do CurseForge e
    /// duplicar uploads idênticos. Não toca no banco — a gravação acontece no Guardar.
    /// </summary>
    public async Task<ModEntryEntity> AddUploadedModAsync(
        string fileName, byte[] content, CancellationToken ct = default)
    {
        if (content.Length == 0) throw new InvalidOperationException("Arquivo vazio.");

        // Path.GetFileName neutraliza qualquer caminho embutido no nome enviado
        var safeName = Path.GetFileName(fileName);
        var hash = SHA1.HashData(content);
        var sha1 = Convert.ToHexString(hash).ToLowerInvariant();

        // FileId determinístico pelo conteúdo, sempre negativo (CurseForge usa positivos)
        var fileId = -Math.Abs(BitConverter.ToInt64(hash, 0));
        if (fileId == 0) fileId = -1; // guarda defensiva contra o caso degenerado

        var dir = Path.Combine(ServerPaths.Mods(_root), fileId.ToString());
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, safeName);
        if (!File.Exists(path)) await File.WriteAllBytesAsync(path, content, ct);

        return new ModEntryEntity
        {
            CurseModId = 0, // sem projeto no CurseForge
            FileId = fileId,
            Name = Path.GetFileNameWithoutExtension(safeName),
            Version = "manual",
            FileName = safeName,
            DownloadUrl = string.Empty, // já está no cache; não há origem remota
            Target = "mod",
            Side = ModSide.Both,
            Sha1 = sha1,
            FileLength = content.Length
        };
    }

    // ── Edição de overrides (arquivos de texto) ──────────────────────────────────────────────────

    // Extensões consideradas texto editável no painel
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".json", ".json5", ".jsonc", ".toml", ".cfg", ".conf", ".config", ".properties",
        ".yaml", ".yml", ".snbt", ".nbt", ".ini", ".lang", ".md", ".mcmeta", ".js", ".zs", ".xml", ".csv"
    };

    /// <summary>É um arquivo de texto que o editor do painel pode abrir?</summary>
    public static bool IsTextOverride(string relativePath)
    {
        return TextExtensions.Contains(Path.GetExtension(relativePath));
    }

    /// <summary>Lista os caminhos relativos dos arquivos de overrides do modpack (ordenados).</summary>
    public IReadOnlyList<string> ListOverrides(Guid uid)
    {
        var dir = OverridesDir(uid);
        if (!Directory.Exists(dir)) return [];

        return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(dir, f).Replace('\\', '/'))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Lista os arquivos de overrides com tamanho, numa única varredura do disco. A página usa isto
    /// para não fazer um <c>stat</c> por arquivo a cada seleção (caro com muitos arquivos) — o
    /// <see cref="FileInfo.Length"/> já vem preenchido pela enumeração de <see cref="DirectoryInfo"/>.
    /// </summary>
    public IReadOnlyList<OverrideFileDto> ListOverrideFiles(Guid uid)
    {
        var dir = OverridesDir(uid);
        if (!Directory.Exists(dir)) return [];

        var baseDir = new DirectoryInfo(dir);
        return baseDir.EnumerateFiles("*", SearchOption.AllDirectories)
            .Select(f => new OverrideFileDto(
                Path.GetRelativePath(dir, f.FullName).Replace('\\', '/'), f.Length))
            .OrderBy(o => o.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Lê o conteúdo de texto de um arquivo de override (null se não existir/for inválido).</summary>
    public async Task<string?> ReadOverrideAsync(Guid uid, string relativePath, CancellationToken ct = default)
    {
        var path = SafeOverridePath(uid, relativePath);
        if (path is null || !File.Exists(path)) return null;
        return await File.ReadAllTextAsync(path, ct);
    }

    /// <summary>
    /// Grava o conteúdo de texto de um arquivo de override existente (escrita direta em disco) e
    /// registra a versão anterior no histórico (para o "desfazer").
    /// </summary>
    public async Task WriteOverrideAsync(
        Guid uid, string relativePath, string content, string? actor = null, CancellationToken ct = default)
    {
        var path = SafeOverridePath(uid, relativePath)
                   ?? throw new InvalidOperationException("Caminho de override inválido.");
        if (!File.Exists(path))
            throw new InvalidOperationException("Arquivo de override não encontrado.");

        // Guarda o conteúdo anterior antes de sobrescrever — é o que o desfazer restaura
        var previous = await File.ReadAllTextAsync(path, ct);
        if (string.Equals(previous, content, StringComparison.Ordinal)) return; // nada mudou

        await File.WriteAllTextAsync(path, content, ct);
        await LogHistoryAsync(uid, OverrideOp.Edit, relativePath, null, previous, actor, ct);
    }

    /// <summary>
    /// Cria um arquivo de override novo (texto, opcionalmente com conteúdo). Cria os diretórios
    /// pais e marca o modpack como tendo overrides. Devolve o estado da flag <c>HasOverrides</c>.
    /// Lança se já existir um arquivo nesse caminho.
    /// </summary>
    public async Task<bool> CreateOverrideAsync(
        Guid uid, string relativePath, string content = "", CancellationToken ct = default)
    {
        var path = SafeOverridePath(uid, relativePath)
                   ?? throw new InvalidOperationException("Caminho de override inválido.");
        if (File.Exists(path))
            throw new InvalidOperationException("Já existe um arquivo nesse caminho.");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content, ct);
        return await SetHasOverridesAsync(uid, true, ct);
    }

    /// <summary>
    /// Grava um arquivo de override enviado pelo admin (texto ou binário). Substitui se já existir,
    /// cria os diretórios pais e marca o modpack como tendo overrides.
    /// </summary>
    public async Task<bool> UploadOverrideAsync(
        Guid uid, string relativePath, Stream content, CancellationToken ct = default)
    {
        var path = SafeOverridePath(uid, relativePath)
                   ?? throw new InvalidOperationException("Caminho de override inválido.");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using (var fs = File.Create(path))
        {
            await content.CopyToAsync(fs, ct);
        }

        return await SetHasOverridesAsync(uid, true, ct);
    }

    /// <summary>
    /// Apaga um arquivo de override e remove os diretórios que ficaram vazios. Reavalia e devolve
    /// a flag <c>HasOverrides</c> (vira <c>false</c> se foi o último arquivo).
    /// </summary>
    public async Task<bool> DeleteOverrideAsync(
        Guid uid, string relativePath, string? actor = null, CancellationToken ct = default)
    {
        var path = SafeOverridePath(uid, relativePath);
        if (path is not null && File.Exists(path))
        {
            // Guarda o conteúdo (se texto) para o desfazer poder recriar o arquivo
            var content = IsTextOverride(relativePath) ? await File.ReadAllTextAsync(path, ct) : null;
            File.Delete(path);
            await LogHistoryAsync(uid, OverrideOp.DeleteFile, relativePath, null, content, actor, ct);
        }

        // Remove pastas que ficaram vazias para a árvore não acumular diretórios fantasma
        CleanEmptyDirectories(OverridesDir(uid));

        var stillHasOverrides = ListOverrides(uid).Count > 0;
        return await SetHasOverridesAsync(uid, stillHasOverrides, ct);
    }

    /// <summary>
    /// Move um arquivo de override para outra pasta (<paramref name="targetFolder"/> vazio = raiz),
    /// preservando o nome. Lança se já houver arquivo de mesmo nome no destino. Devolve o novo
    /// caminho relativo. Remove diretórios que ficaram vazios.
    /// </summary>
    public async Task<string> MoveOverrideAsync(
        Guid uid, string sourceRelPath, string targetFolder, string? actor = null, CancellationToken ct = default)
    {
        var src = SafeOverridePath(uid, sourceRelPath)
                  ?? throw new InvalidOperationException("Caminho de origem inválido.");
        if (!File.Exists(src))
            throw new InvalidOperationException("Arquivo de origem não encontrado.");

        var fileName = Path.GetFileName(sourceRelPath);
        var destRel = string.IsNullOrEmpty(targetFolder) ? fileName : $"{targetFolder}/{fileName}";

        var dest = SafeOverridePath(uid, destRel)
                   ?? throw new InvalidOperationException("Pasta de destino inválida.");
        if (string.Equals(dest, src, StringComparison.OrdinalIgnoreCase)) return destRel; // mesmo lugar
        if (File.Exists(dest))
            throw new InvalidOperationException("Já existe um arquivo com esse nome na pasta de destino.");

        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.Move(src, dest);
        CleanEmptyDirectories(OverridesDir(uid));

        await LogHistoryAsync(uid, OverrideOp.MoveFile, sourceRelPath, destRel, null, actor, ct);
        await SetHasOverridesAsync(uid, ListOverrides(uid).Count > 0, ct);
        return destRel;
    }

    /// <summary>
    /// Move uma pasta de overrides inteira para dentro de <paramref name="targetFolder"/> (vazio =
    /// raiz), preservando o nome da pasta. Recusa mover para dentro de si mesma ou para um destino
    /// já ocupado. Devolve o novo caminho da pasta. Registra no histórico (desfazer).
    /// </summary>
    public async Task<string> MoveOverrideFolderAsync(
        Guid uid, string sourceFolder, string targetFolder, string? actor = null, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(sourceFolder))
            throw new InvalidOperationException("Pasta de origem inválida.");

        var folderName = sourceFolder.Contains('/')
            ? sourceFolder[(sourceFolder.LastIndexOf('/') + 1)..]
            : sourceFolder;
        var destFolder = string.IsNullOrEmpty(targetFolder) ? folderName : $"{targetFolder}/{folderName}";

        if (string.Equals(destFolder, sourceFolder, StringComparison.OrdinalIgnoreCase))
            return sourceFolder; // já está aqui
        // Não permitir mover uma pasta para dentro dela mesma (criaria recursão infinita)
        if (destFolder.StartsWith(sourceFolder + "/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Não dá para mover uma pasta para dentro dela mesma.");

        var src = SafeOverridePath(uid, sourceFolder)
                  ?? throw new InvalidOperationException("Pasta de origem inválida.");
        if (!Directory.Exists(src))
            throw new InvalidOperationException("Pasta de origem não encontrada.");

        var dest = SafeOverridePath(uid, destFolder)
                   ?? throw new InvalidOperationException("Pasta de destino inválida.");
        if (Directory.Exists(dest))
            throw new InvalidOperationException("Já existe uma pasta com esse nome no destino.");

        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        Directory.Move(src, dest);
        CleanEmptyDirectories(OverridesDir(uid));

        await LogHistoryAsync(uid, OverrideOp.MoveFolder, sourceFolder, destFolder, null, actor, ct);
        await SetHasOverridesAsync(uid, ListOverrides(uid).Count > 0, ct);
        return destFolder;
    }

    /// <summary>
    /// Apaga uma pasta de overrides inteira (recursivo) e remove os diretórios vazios remanescentes.
    /// Reavalia e devolve a flag <c>HasOverrides</c>. (Exclusão de pasta não é desfazível.)
    /// </summary>
    public async Task<bool> DeleteOverrideFolderAsync(Guid uid, string folderRelPath, CancellationToken ct = default)
    {
        var dir = SafeOverridePath(uid, folderRelPath);
        if (dir is not null && Directory.Exists(dir)) Directory.Delete(dir, true);

        CleanEmptyDirectories(OverridesDir(uid));
        return await SetHasOverridesAsync(uid, ListOverrides(uid).Count > 0, ct);
    }

    // ── Histórico / desfazer ─────────────────────────────────────────────────────────────────────

    /// <summary>Registra uma operação no histórico de overrides do modpack.</summary>
    private async Task LogHistoryAsync(
        Guid uid, OverrideOp op, string? before, string? after, string? content, string? actor, CancellationToken ct)
    {
        db.OverrideHistory.Add(new OverrideHistoryEntry
        {
            ModpackId = uid, Operation = op, PathBefore = before, PathAfter = after,
            ContentBefore = content, Actor = actor, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>A entrada mais recente do histórico do modpack (null se não há o que desfazer).</summary>
    public Task<OverrideHistoryEntry?> GetLastHistoryAsync(Guid uid, CancellationToken ct = default)
    {
        return db.OverrideHistory.AsNoTracking()
            .Where(h => h.ModpackId == uid)
            .OrderByDescending(h => h.CreatedAt).ThenByDescending(h => h.Id)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>Histórico recente do modpack (mais novo primeiro) para a modal de desfazer.</summary>
    public async Task<List<OverrideHistoryEntry>> GetHistoryAsync(
        Guid uid, int take = 100, CancellationToken ct = default)
    {
        return await db.OverrideHistory.AsNoTracking()
            .Where(h => h.ModpackId == uid)
            .OrderByDescending(h => h.CreatedAt).ThenByDescending(h => h.Id)
            .Take(take)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Desfaz a última operação registrada (aplica a inversa e remove a entrada). Devolve a entrada
    /// desfeita (para a UI descrever o que aconteceu) ou null se não havia nada.
    /// </summary>
    public async Task<OverrideHistoryEntry?> UndoLastAsync(Guid uid, CancellationToken ct = default)
    {
        var entry = await db.OverrideHistory
            .Where(h => h.ModpackId == uid)
            .OrderByDescending(h => h.CreatedAt).ThenByDescending(h => h.Id)
            .FirstOrDefaultAsync(ct);
        if (entry is null) return null;

        await ApplyInverseAsync(uid, entry, ct);
        db.OverrideHistory.Remove(entry); // entry é rastreada (carregada sem AsNoTracking)
        await db.SaveChangesAsync(ct);
        await SetHasOverridesAsync(uid, ListOverrides(uid).Count > 0, ct);
        return entry;
    }

    /// <summary>
    /// Desfaz do mais recente até (inclusive) a entrada <paramref name="entryId"/>. Como o histórico
    /// é uma pilha, desfazer uma ação antiga exige desfazer as mais novas primeiro. Devolve quantas
    /// foram desfeitas (0 se a entrada já não existir).
    /// </summary>
    public async Task<int> UndoToAsync(Guid uid, int entryId, CancellationToken ct = default)
    {
        // A entrada-alvo ainda existe? (pode já ter sido desfeita por outra via)
        if (!await db.OverrideHistory.AnyAsync(h => h.ModpackId == uid && h.Id == entryId, ct))
            return 0;

        var count = 0;
        while (true)
        {
            var undone = await UndoLastAsync(uid, ct);
            if (undone is null) break;
            count++;
            if (undone.Id == entryId) break; // chegou na entrada escolhida
        }

        return count;
    }

    // Aplica a operação inversa de uma entrada do histórico (não mexe no banco nem na flag)
    private async Task ApplyInverseAsync(Guid uid, OverrideHistoryEntry entry, CancellationToken ct)
    {
        switch (entry.Operation)
        {
            case OverrideOp.Edit:
                // Restaura o conteúdo anterior (se o arquivo ainda existe)
                var editPath = SafeOverridePath(uid, entry.PathBefore!);
                if (editPath is not null && File.Exists(editPath) && entry.ContentBefore is not null)
                    await File.WriteAllTextAsync(editPath, entry.ContentBefore, ct);
                break;

            case OverrideOp.DeleteFile:
                // Recria o arquivo excluído com o conteúdo guardado
                var recreatePath = SafeOverridePath(uid, entry.PathBefore!);
                if (recreatePath is not null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(recreatePath)!);
                    await File.WriteAllTextAsync(recreatePath, entry.ContentBefore ?? string.Empty, ct);
                }

                break;

            case OverrideOp.MoveFile:
                MoveBack(uid, entry.PathAfter!, entry.PathBefore!, false);
                break;

            case OverrideOp.MoveFolder:
                MoveBack(uid, entry.PathAfter!, entry.PathBefore!, true);
                break;
        }

        CleanEmptyDirectories(OverridesDir(uid));
    }

    // Move um arquivo/pasta de volta (usado pelo desfazer); ignora silenciosamente se a origem sumiu
    private void MoveBack(Guid uid, string from, string to, bool isFolder)
    {
        var src = SafeOverridePath(uid, from);
        var dst = SafeOverridePath(uid, to);
        if (src is null || dst is null) return;

        Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
        if (isFolder)
        {
            if (Directory.Exists(src) && !Directory.Exists(dst)) Directory.Move(src, dst);
        }
        else
        {
            if (File.Exists(src) && !File.Exists(dst)) File.Move(src, dst);
        }
    }

    /// <summary>Tamanho em bytes de um arquivo de override (0 se não existir).</summary>
    public long GetOverrideLength(Guid uid, string relativePath)
    {
        var path = SafeOverridePath(uid, relativePath);
        return path is not null && File.Exists(path) ? new FileInfo(path).Length : 0;
    }

    /// <summary>
    /// Atualiza a flag <c>HasOverrides</c> do modpack no banco (se mudou) e avisa os launchers (SSE).
    /// Devolve o valor final. No-op silencioso se o modpack ainda não existir na BD (rascunho novo).
    /// </summary>
    private async Task<bool> SetHasOverridesAsync(Guid uid, bool value, CancellationToken ct)
    {
        var row = await db.Modpacks.FirstOrDefaultAsync(m => m.Id == uid, ct);
        if (row is null || row.HasOverrides == value) return value;

        row.HasOverrides = value;
        row.UpdatedAt = DateTime.UtcNow; // marca a alteração para sync incremental do launcher
        await db.SaveChangesAsync(ct);
        notifier.Bump();
        return value;
    }

    /// <summary>Remove recursivamente os subdiretórios vazios sob <paramref name="root"/> (mantém a raiz).</summary>
    private static void CleanEmptyDirectories(string root)
    {
        if (!Directory.Exists(root)) return;

        // Ordena do mais profundo para o mais raso para que um pai esvaziado também seja removido
        foreach (var dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories)
                     .OrderByDescending(d => d.Length))
            if (!Directory.EnumerateFileSystemEntries(dir).Any())
                Directory.Delete(dir);
    }

    private string OverridesDir(Guid uid)
    {
        return Path.Combine(ServerPaths.Modpacks(_root), uid.ToString(), "overrides");
    }

    /// <summary>
    /// Resolve o caminho absoluto de um override garantindo que fica <b>dentro</b> da pasta do modpack
    /// (defesa contra path traversal). Devolve null se escapar do diretório base.
    /// </summary>
    private string? SafeOverridePath(Guid uid, string relativePath)
    {
        var baseDir = Path.GetFullPath(OverridesDir(uid));
        var full = Path.GetFullPath(Path.Combine(baseDir, relativePath));

        // O caminho final precisa começar pela base (com separador) para não escapar via "../"
        var baseWithSep = baseDir.EndsWith(Path.DirectorySeparatorChar)
            ? baseDir
            : baseDir + Path.DirectorySeparatorChar;
        return full.StartsWith(baseWithSep, StringComparison.Ordinal) ? full : null;
    }
}