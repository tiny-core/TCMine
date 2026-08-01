using Microsoft.EntityFrameworkCore;
using TCMine.Server.Application.Abstractions;
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
            .FirstOrDefaultAsync(v => v.Id == versionId, ct);
    }

    public async Task<IReadOnlyList<ModpackVersion>> ListVersionsAsync(Guid modpackId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.ModpackVersions
            .AsNoTracking()
            .Include(v => v.Files)
            .Where(v => v.ModpackId == modpackId)
            .OrderByDescending(v => v.Id)
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
            db.Entry(file).State = existingIds.Contains(file.Id)
                ? EntityState.Unchanged
                : EntityState.Added;

        await db.SaveChangesAsync(ct);
    }

    public async Task<Modpack?> GetWithVersionsAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Modpacks
            .AsNoTracking()
            .Include(m => m.Versions)
            .ThenInclude(v => v.Files)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
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