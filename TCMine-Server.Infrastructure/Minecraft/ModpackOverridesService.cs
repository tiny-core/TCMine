using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using TCMine_Application.Contracts;
using TCMine_Server.Infrastructure.FileSystem;
using TCMine_Server.Infrastructure.Persistence;
using TCMine_Domain.Entities;
using TCMine_Server.Infrastructure.Server;

namespace TCMine_Server.Infrastructure.Minecraft;

/// <summary>
/// Gestão dos <b>overrides</b> de um modpack: a árvore de arquivos editável no painel
/// (<c>tcmine-data/modpacks/{slug}/overrides/</c>) e o histórico de operações com desfazer. Extraído do
/// <see cref="ModpackImportService"/> (que ficou grande demais): o import/save ainda extrai o bundle
/// inicial, mas toda a edição interativa — criar/ler/gravar/mover/apagar arquivos e pastas, mais o
/// undo — vive aqui, com o seu próprio conjunto de consumidores (a <c>OverridesPanel</c> e a árvore).
///
/// Os arquivos são a fonte da verdade em disco; a flag <see cref="Domain.Entities.ModpackEntity.HasOverrides"/>
/// e o <see cref="OverrideHistoryEntry"/> vivem no banco. Toda alteração bumpa o <see cref="ContentNotifier"/>
/// (SSE) e marca o modpack como alterado (sync incremental do launcher). Todo caminho passa por
/// <see cref="SafeOverridePath"/> — defesa contra path traversal para fora da pasta do modpack.
/// </summary>
public sealed class ModpackOverridesService(AppDbContext db, ContentNotifier notifier, IHostEnvironment env)
{
    private readonly string _root = env.ContentRootPath;

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

    /// <summary>
    /// Lista os **filhos diretos** (um nível) de uma pasta de overrides — para o carregamento
    /// preguiçoso da árvore. <paramref name="relativeFolder"/> vazio = raiz. Pastas primeiro, depois
    /// arquivos, cada grupo em ordem alfabética. Não recursivo.
    /// </summary>
    public IReadOnlyList<OverrideNodeDto> ListOverrideChildren(Guid uid, string relativeFolder)
    {
        // Raiz usa OverridesDir direto (SafeOverridePath recusa caminho vazio); subpastas são validadas
        var dir = string.IsNullOrEmpty(relativeFolder)
            ? OverridesDir(uid)
            : SafeOverridePath(uid, relativeFolder);
        if (dir is null || !Directory.Exists(dir)) return [];

        var prefix = string.IsNullOrEmpty(relativeFolder) ? "" : relativeFolder.TrimEnd('/') + "/";

        var folders = Directory.EnumerateDirectories(dir)
            .Select(d => Path.GetFileName(d))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => new OverrideNodeDto(prefix + name, name, true));

        var files = Directory.EnumerateFiles(dir)
            .Select(f => Path.GetFileName(f))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => new OverrideNodeDto(prefix + name, name, false));

        return folders.Concat(files).ToList();
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
