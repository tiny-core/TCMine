using Microsoft.EntityFrameworkCore;
using TCMine_Application.Contracts;
using TCMine_Application.Modpack;
using TCMine_Domain.Modpack;
using TCMine_Server.Infrastructure.Persistence;
using TCMine_Domain.Entities;
using TCMine_Server.Infrastructure.CurseForge;

namespace TCMine_Server.Infrastructure.Minecraft;

/// <summary>
/// Checagem <b>econômica</b> de atualizações contra o CurseForge — do modpack (origem 1:1) e dos seus
/// mods. Extraído do <see cref="ModpackImportService"/>: é uma preocupação de leitura pura (nunca é
/// chamada dentro do fluxo de gravação), consumida só pela UI do editor.
///
/// - <b>Modpack</b>: usa a origem do import (<see cref="ModpackImportSourceEntity"/>) com cache/TTL de
///   6h no banco (não rebate na API a cada abertura).
/// - <b>Mods</b>: sob demanda, sem cache — 1 batch de <c>latestFilesIndexes</c> + 1 batch de arquivos
///   só para os que mudaram.
/// </summary>
public sealed class ModpackUpdateService(AppDbContext db, CurseForgeApiClient cf)
{
    /// <summary>Origem CF do modpack (ou null se não foi importado do CurseForge).</summary>
    public Task<ModpackImportSourceEntity?> GetImportSourceAsync(Guid modpackId, CancellationToken ct = default)
    {
        return db.ModpackImportSources.AsNoTracking().FirstOrDefaultAsync(s => s.ModpackId == modpackId, ct);
    }

    /// <summary>
    /// Verifica se o modpack importado tem versão nova no CF. Respeita um TTL (não rebate na API se
    /// checou recentemente) salvo <paramref name="force"/>. Atualiza o cache (LatestFileId/Version/
    /// LastCheckedAt) e devolve o estado, ou null se o modpack não tem origem CF.
    /// </summary>
    public async Task<ModpackUpdateStatusDto?> CheckModpackUpdateAsync(
        Guid modpackId, bool force = false, CancellationToken ct = default)
    {
        var src = await db.ModpackImportSources.FirstOrDefaultAsync(s => s.ModpackId == modpackId, ct);
        if (src is null) return null;

        // TTL de 6h: evita bater na API a cada abertura; o botão "checar agora" passa force=true
        var fresh = src.LastCheckedAt is { } t && DateTime.UtcNow - t < TimeSpan.FromHours(6);
        if (!force && fresh) return ToStatus(src);

        var latest = await cf.GetLatestFileAsync(src.CurseProjectId, ct);
        src.LatestFileId = latest?.Id;
        src.LatestVersion = latest?.DisplayName ?? latest?.FileName;
        src.LastCheckedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return ToStatus(src);
    }

    private static ModpackUpdateStatusDto ToStatus(ModpackImportSourceEntity s)
    {
        return new ModpackUpdateStatusDto(
            s.CurseProjectName, s.InstalledVersion, s.InstalledFileId,
            s.LatestFileId, s.LatestVersion, s.UpdateAvailable, s.LastCheckedAt);
    }

    /// <summary>
    /// Procura atualizações para os mods do CurseForge (CurseModId &gt; 0) do rascunho, para a versão
    /// MC + loader dados. **Sob demanda**, econômico: 1 batch de <c>latestFilesIndexes</c> para todos
    /// os mods, e 1 batch de arquivos só para os que mudaram (resolve url/nome). Sem cache no banco.
    /// </summary>
    public async Task<List<ModUpdateDto>> CheckModUpdatesAsync(
        IReadOnlyList<ModEntryEntity> mods, string gameVersion, ModLoader loader,
        CancellationToken ct = default)
    {
        // Um mod (CurseModId) costuma ter um arquivo por pack; em caso de repetição, fica o primeiro
        var current = mods
            .Where(m => m.CurseModId > 0)
            .GroupBy(m => m.CurseModId)
            .ToDictionary(g => g.Key, g => g.First());
        if (current.Count == 0) return [];

        var indexes = await cf.GetLatestFileIndexesAsync(
            current.Keys.ToList(), gameVersion, CurseForgeApiClient.ModLoaderType(loader), ct);

        // Mods cujo arquivo mais recente difere do instalado
        var changed = indexes
            .Where(kv => current.TryGetValue(kv.Key, out var c) && kv.Value.FileId != c.FileId)
            .Select(kv => kv.Value)
            .ToList();
        if (changed.Count == 0) return [];

        // Resolve url/nome/versão dos novos arquivos num único batch
        var details = await cf.GetFilesAsync(changed.Select(c => c.FileId).ToList(), ct);

        var updates = new List<ModUpdateDto>();
        foreach (var idx in changed)
        {
            var cur = current[idx.ModId];
            details.TryGetValue(idx.FileId, out var fd);
            var url = CurseForgeImporter.ResolveDownloadUrl(fd) ?? string.Empty;
            if (string.IsNullOrEmpty(url)) continue; // sem download público utilizável → ignora

            updates.Add(new ModUpdateDto(
                idx.ModId, cur.Name, cur.FileId, cur.Version,
                idx.FileId, fd?.DisplayName ?? idx.FileName, fd?.FileName ?? idx.FileName, url));
        }

        return updates;
    }
}
