using Microsoft.EntityFrameworkCore;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Infrastructure.Persistence;

/// <summary>
///     Repositório de modpacks sobre uma factory de DbContext.
///     Cada método cria um contexto curto e o descarta ao fim. Padrão correto
///     para Blazor Server, onde o scope de DI dura a conexão inteira — um
///     DbContext scoped acumularia entidades de todas as telas e colidiria.
/// </summary>
public sealed class ModpackRepository(IDbContextFactory<TcMineDbContext> factory) : IModpackRepository
{
    public async Task<bool> SlugExistsAsync(string slug, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Modpacks.AnyAsync(m => m.Slug == slug, ct);
    }

    public async Task<bool> ExistsFromUpstreamAsync(ModFileOrigin origin, string projectId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Modpacks
            .AnyAsync(m => m.UpstreamProvider == origin && m.UpstreamProjectId == projectId, ct);
    }

    public async Task<IReadOnlyDictionary<Guid, ModpackVersionStats>> GetVersionStatsAsync(
        Guid modpackId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // GroupBy no banco: volta uma linha por versão, não uma por arquivo.
        var rows = await db.ModpackFiles
            .AsNoTracking()
            .Where(f => db.ModpackVersions
                .Where(v => v.ModpackId == modpackId)
                .Select(v => v.Id)
                .Contains(f.ModpackVersionId))
            .GroupBy(f => f.ModpackVersionId)
            .Select(g => new
            {
                VersionId = g.Key,
                ModCount = g.Count(f => f.Origin != ModFileOrigin.Override),
                OverrideCount = g.Count(f => f.Origin == ModFileOrigin.Override),
                TotalSizeBytes = g.Sum(f => f.SizeBytes)
            })
            .ToListAsync(ct);

        return rows.ToDictionary(
            r => r.VersionId,
            r => new ModpackVersionStats(r.ModCount, r.OverrideCount, r.TotalSizeBytes));
    }

    public async Task<Modpack?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Modpacks
            .AsNoTracking()
            .Include(m => m.Versions)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task RemoveAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Modpacks.Where(m => m.Id == id).ExecuteDeleteAsync(ct);
    }

    public async Task RemoveVersionAsync(Guid versionId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.ModpackVersions.Where(v => v.Id == versionId).ExecuteDeleteAsync(ct);
    }

    public async Task UpdateAsync(Modpack modpack, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Modpacks.Update(modpack);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Modpack>> ListAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Modpacks
            .AsNoTracking()
            .OrderBy(m => m.Name)
            .ToListAsync(ct);
    }

    public async Task<ModpackVersion?> GetVersionAsync(Guid versionId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.ModpackVersions
            .AsNoTracking()
            .Include(v => v.Files)
            .Include(v => v.PendingMods)
            .AsSplitQuery()
            .FirstOrDefaultAsync(v => v.Id == versionId, ct);
    }

    public async Task<IReadOnlyList<ModpackVersion>> ListVersionsAsync(Guid modpackId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        // Split pelo mesmo motivo do GetWithVersionsAsync: numa consulta única os
        // dados da versão se repetem em cada linha de arquivo.
        return await db.ModpackVersions
            .AsNoTracking()
            .Include(v => v.Files)
            .Where(v => v.ModpackId == modpackId)
            .OrderByDescending(v => v.Id)
            .AsSplitQuery()
            .ToListAsync(ct);
    }

    public async Task CreateAsync(Modpack modpack, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Modpacks.Add(modpack);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddVersionAsync(ModpackVersion version, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.ModpackVersions.Add(version);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateVersionAsync(ModpackVersion version, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // Anexa a versão e deixa o EF descobrir o que mudou comparando com o
        // que está no banco. Update marca tudo como Modified de uma vez, mas
        // com filhos novos no grafo o resultado é imprevisível — attach +
        // detecção de estado por ID é mais confiável.
        db.Attach(version);

        // A versão em si mudou (estado, timestamps).
        db.Entry(version).State = EntityState.Modified;

        // Cada arquivo: se o EF não conhece o Id, é novo (Added); se conhece,
        // ficou Unchanged ao anexar e não precisa de UPDATE.
        var existingIds = await db.ModpackFiles
            .Where(f => f.ModpackVersionId == version.Id)
            .Select(f => f.Id)
            .ToListAsync(ct);

        foreach (var file in version.Files)
        {
            db.Entry(file).State = existingIds.Contains(file.Id)
                ? EntityState.Modified // existente: pode ter mudado (move, edição)
                : EntityState.Added; // novo: INSERT
        }

        // Mesma regra para as pendências.
        var existingPendingIds = await db.PendingMods
            .Where(p => p.ModpackVersionId == version.Id)
            .Select(p => p.Id)
            .ToListAsync(ct);

        foreach (var pending in version.PendingMods)
        {
            db.Entry(pending).State = existingPendingIds.Contains(pending.Id)
                ? EntityState.Modified
                : EntityState.Added;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<Modpack?> GetWithVersionsAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        // AsSplitQuery: sem isto o EF junta versões × arquivos numa consulta só e
        // repete os dados da versão em cada linha de arquivo. Num pack importado
        // (centenas de mods, milhares de overrides) esse produto cartesiano faz a
        // página de detalhe levar dezenas de segundos. Consultas separadas trazem
        // o mesmo grafo sem a explosão.
        return await db.Modpacks
            .AsNoTracking()
            .Include(m => m.Versions)
            .ThenInclude(v => v.Files)
            .Include(m => m.Versions)
            .ThenInclude(v => v.PendingMods)
            .AsSplitQuery()
            .FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<PagedResult<ModInventoryEntry>> ListModInventoryAsync(
        ModInventoryQuery query, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var files = db.ModpackFiles
            .AsNoTracking()
            .Where(f => f.Origin != ModFileOrigin.Override && f.ProjectSlug != null);

        if (query.Origin is { } origin)
            files = files.Where(f => f.Origin == origin);

        if (query.Search is { Length: > 0 } search)
            files = files.Where(f => f.Path.Contains(search) || f.ProjectSlug!.Contains(search));

        // Agrupa por mod. O HAVING do filtro de órfão fica AQUI, no banco: só
        // depois de agrupar dá para saber se sobrou alguma referência ativa.
        var grouped = files
            .Join(db.ModpackVersions, f => f.ModpackVersionId, v => v.Id, (f, v) => new { File = f, Version = v })
            .GroupBy(x => new { Slug = x.File.ProjectSlug!, x.File.Origin });

        if (query.OnlyOrphans)
            grouped = grouped.Where(g => g.Count(x => x.Version.State != ModpackVersionState.Archived) == 0);

        var projected = grouped.Select(g => new
        {
            g.Key.Slug,
            g.Key.Origin,

            // Max: o mesmo mod tem nomes de arquivo diferentes entre versões
            // (jei-1.2.jar, jei-1.5.jar). Qualquer um serve para exibir.
            Name = g.Max(x => x.File.Path),
            IconUrl = g.Max(x => x.File.IconUrl),
            SizeBytes = g.Max(x => x.File.SizeBytes),
            TotalReferences = g.Count(),
            ActiveReferences = g.Count(x => x.Version.State != ModpackVersionState.Archived)
        });

        var total = await projected.CountAsync(ct);

        var rows = await projected
            .OrderBy(r => r.Name)
            .ThenBy(r => r.Slug) // desempate estável: sem isto a paginação repete linhas
            .Skip(query.Page.Skip)
            .Take(query.Page.PageSize)
            .ToListAsync(ct);

        // Os donos só da página corrente. Buscar de todos os mods do catálogo
        // para exibir 25 seria o mesmo desperdício que a paginação evita.
        var slugs = rows.Select(r => r.Slug).ToList();

        var owners = await db.ModpackFiles
            .AsNoTracking()
            .Where(f => f.ProjectSlug != null && slugs.Contains(f.ProjectSlug))
            .Join(db.ModpackVersions, f => f.ModpackVersionId, v => v.Id, (f, v) => new { f.ProjectSlug, v.ModpackId })
            .Join(db.Modpacks, x => x.ModpackId, m => m.Id, (x, m) => new { Slug = x.ProjectSlug!, m.Name })
            .Distinct()
            .ToListAsync(ct);

        var byslug = owners
            .GroupBy(o => o.Slug, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)[.. g.Select(o => o.Name).Order()],
                StringComparer.OrdinalIgnoreCase);

        var items = rows
            .Select(r => new ModInventoryEntry(
                r.Slug,
                FileNameOf(r.Name ?? r.Slug),
                r.Origin,
                r.IconUrl,
                r.SizeBytes,
                byslug.GetValueOrDefault(r.Slug, []),
                r.ActiveReferences,
                r.TotalReferences))
            .ToList();

        return new PagedResult<ModInventoryEntry>(items, total);
    }

    public async Task<PagedResult<ModpackFile>> ListVersionModsAsync(
        Guid versionId, string? search, PageRequest page, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var q = db.ModpackFiles
            .AsNoTracking()
            .Where(f => f.ModpackVersionId == versionId && f.Origin != ModFileOrigin.Override);

        if (search is { Length: > 0 })
            q = q.Where(f => f.Path.Contains(search));

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderBy(f => f.Path)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(ct);

        return new PagedResult<ModpackFile>(items, total);
    }

    // "mods/jei-1.5.jar" → "jei-1.5.jar". O caminho completo não acrescenta nada
    // numa tabela onde tudo mora em mods/.
    private static string FileNameOf(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash < 0 ? path : path[(slash + 1)..];
    }

    public async Task<IReadOnlySet<string>> ListReferencedHashesAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var fileHashes = await db.ModpackFiles
            .AsNoTracking()
            .Select(f => f.Sha256)
            .Distinct()
            .ToListAsync(ct);

        var iconHashes = await db.Modpacks
            .AsNoTracking()
            .Where(m => m.IconBlobSha256 != null)
            .Select(m => m.IconBlobSha256!)
            .Distinct()
            .ToListAsync(ct);

        return fileHashes.Concat(iconHashes).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task AddFilesAsync(Guid versionId, IReadOnlyList<ModpackFile> files, CancellationToken ct)
    {
        if (files.Count is 0)
            return;

        await using var db = await factory.CreateDbContextAsync(ct);

        foreach (var file in files)
            file.ModpackVersionId = versionId;

        // AddRange + um SaveChanges: uma transação só para o lote inteiro.
        db.ModpackFiles.AddRange(files);
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveVersionStateAsync(ModpackVersion version, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        db.Attach(version);
        db.Entry(version).State = EntityState.Modified;

        // Anexar a versão arrasta a coleção de arquivos junto. Marcá-los
        // Unchanged é o ponto deste método: eles já foram gravados enquanto a
        // ingestão corria, e reescrever milhares de linhas idênticas no fim é
        // exatamente o custo que se quer evitar.
        foreach (var file in version.Files)
            db.Entry(file).State = EntityState.Unchanged;

        var existingPendingIds = await db.PendingMods
            .Where(p => p.ModpackVersionId == version.Id)
            .Select(p => p.Id)
            .ToListAsync(ct);

        foreach (var pending in version.PendingMods)
        {
            db.Entry(pending).State = existingPendingIds.Contains(pending.Id)
                ? EntityState.Modified
                : EntityState.Added;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task RemovePendingAsync(Guid versionId, Guid pendingId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.PendingMods
            .Where(p => p.Id == pendingId && p.ModpackVersionId == versionId)
            .ExecuteDeleteAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> ListInterruptedIngestionIdsAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.ModpackVersions
            .AsNoTracking()
            .Where(v => v.State == ModpackVersionState.Resolving
                        || (v.State == ModpackVersionState.Draft
                            && v.PendingMods.Any(p => p.Reason == PendingModReason.Queued)))
            // Guid v7 e cronologico: recupera na ordem em que foi pedido.
            .OrderBy(v => v.Id)
            .Select(v => v.Id)
            .ToListAsync(ct);
    }

    public async Task RemoveFileAsync(Guid versionId, Guid fileId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // Remove só o vínculo do arquivo com a versão. O blob em si permanece
        // no store — pode estar em uso por outra versão, e a limpeza de blobs
        // órfãos é uma rotina separada.
        var file = await db.ModpackFiles
            .FirstOrDefaultAsync(f => f.Id == fileId && f.ModpackVersionId == versionId, ct);

        if (file is not null)
        {
            db.ModpackFiles.Remove(file);
            await db.SaveChangesAsync(ct);
        }
    }
}
