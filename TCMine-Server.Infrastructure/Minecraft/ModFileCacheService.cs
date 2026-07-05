using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using TCMine_Application.Contracts;
using TCMine_Application.Modpack;
using TCMine_Domain.Entities;
using TCMine_Domain.Modpack;
using TCMine_Server.Infrastructure.CurseForge;
using TCMine_Server.Infrastructure.FileSystem;
using TCMine_Server.Infrastructure.Persistence;

namespace TCMine_Server.Infrastructure.Minecraft;

/// <summary>
///     Cache de jars no disco (<c>tcmine-data/mods/{fileId}/{fileName}</c>) e gestão dos
///     <see cref="ModFileEntity" /> partilhados entre modpacks. Extraído do <see cref="ModpackImportService" />
///     para isolar a preocupação "arquivo/jar/hash/órfão" do fluxo de edição do modpack.
///     Princípio: cada jar é baixado <b>uma vez</b> para o cache partilhado, com SHA-1 (integridade) e
///     tamanho; o launcher depois baixa do servidor (nunca do CF). Um <see cref="ModFileEntity" /> pode
///     ser reusado por vários modpacks — quando fica sem nenhum vínculo, é marcado como órfão
///     (<see cref="MarkOrphansAsync" />) e pode ser removido pela página de mods.
/// </summary>
public sealed class ModFileCacheService(AppDbContext db, CurseForgeApiClient cf, IHostEnvironment env)
{
    private readonly string _root = env.ContentRootPath;

    /// <summary>
    ///     Todos os arquivos de mod do servidor (um por FileId) com os modpacks em que aparecem — para a
    ///     página "todos os mods". Órfão = sem nenhum vínculo. Ordenado por nome.
    /// </summary>
    public async Task<List<ModFileRowDto>> ListModFilesAsync(CancellationToken ct = default)
    {
        return await db.ModFiles
            .AsNoTracking()
            .OrderBy(f => f.Name)
            .Select(f => new ModFileRowDto(
                f.FileId, f.Name, f.Version, f.FileName, f.FileLength,
                f.CurseModId <= 0, // upload manual (sem origem CurseForge)
                !f.ModpackLinks.Any(), // órfão = sem vínculo (preciso mesmo se o marcador atrasar)
                f.ModpackLinks
                    .OrderBy(l => l.Modpack!.Name)
                    .Select(l => new ModpackBadgeDto(l.ModpackId, l.Modpack!.Name))
                    .ToList()))
            .ToListAsync(ct);
    }

    /// <summary>
    ///     Remove um arquivo de mod **órfão** (sem vínculos) do banco e o jar do cache de disco. Recusa
    ///     se ainda houver algum modpack usando-o (proteção contra quebrar um pack).
    /// </summary>
    public async Task<bool> DeleteOrphanFileAsync(long fileId, CancellationToken ct = default)
    {
        var stillLinked = await db.ModpackMods.AnyAsync(l => l.FileId == fileId, ct);
        if (stillLinked) return false;

        var file = await db.ModFiles.FirstOrDefaultAsync(f => f.FileId == fileId, ct);
        if (file is null) return false;

        db.ModFiles.Remove(file);
        await db.SaveChangesAsync(ct);

        // Remove o jar do cache compartilhado (data-server/mods/{fileId}/)
        var dir = Path.Combine(ServerPaths.Mods(_root), fileId.ToString());
        if (Directory.Exists(dir)) Directory.Delete(dir, true);

        return true;
    }

    /// <summary>
    ///     Recalcula o marcador de órfão dos arquivos dados: sem nenhum vínculo restante ⇒ OrphanedAt = agora
    ///     (se ainda não marcado); com vínculo ⇒ limpa. Persiste se algo mudou. Chamado pelo
    ///     <see cref="ModpackImportService" /> após gravar/apagar um modpack.
    /// </summary>
    public async Task MarkOrphansAsync(List<long> fileIds, CancellationToken ct)
    {
        if (fileIds.Count == 0) return;

        var linkedIds = await db.ModpackMods
            .Where(l => fileIds.Contains(l.FileId))
            .Select(l => l.FileId)
            .Distinct()
            .ToListAsync(ct);
        var linked = linkedIds.ToHashSet();

        var files = await db.ModFiles
            .Where(f => fileIds.Contains(f.FileId))
            .ToListAsync(ct);

        var changed = false;
        foreach (var file in files)
            if (!linked.Contains(file.FileId) && file.OrphanedAt is null)
            {
                file.OrphanedAt = DateTime.UtcNow;
                changed = true;
            }
            else if (linked.Contains(file.FileId) && file.OrphanedAt is not null)
            {
                file.OrphanedAt = null;
                changed = true;
            }

        if (changed) await db.SaveChangesAsync(ct);
    }

    /// <summary>
    ///     Garante que o jar está no cache (<c>data-server/mods/{fileId}/{fileName}</c>) e devolve
    ///     o SHA-1 e o tamanho. Se já estiver em cache, só recalcula o hash a partir do disco.
    /// </summary>
    public async Task<(string? Sha1, long Length)> EnsureCachedAsync(
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

    /// <summary>
    ///     Baixa o server pack (se houver) e devolve o conjunto de nomes de jar contidos nele —
    ///     base da inferência de <c>ModSide</c>. Devolve nulo quando não há server pack.
    /// </summary>
    public async Task<IReadOnlySet<string>?> LoadServerPackFileNamesAsync(
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
    ///     Recebe um jar enviado pelo admin, guarda-o no cache compartilhado e devolve uma entidade de mod
    ///     <b>destacada</b> (pronta para entrar no rascunho), já com SHA-1/tamanho. O <c>FileId</c> é
    ///     sintético e <b>negativo</b> (derivado do conteúdo) para nunca colidir com ids do CurseForge e
    ///     duplicar uploads idênticos. Não toca no banco — a gravação acontece no Guardar.
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
}