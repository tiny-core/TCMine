using Microsoft.EntityFrameworkCore;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Infrastructure.Persistence;

/// <summary>
///     Implementação do repositório sobre o DbContext.
///     Fina de propósito: traduz os métodos da porta para consultas EF. Toda a
///     regra de negócio está no caso de uso e no domínio — aqui é só acesso a
///     dados.
/// </summary>
public sealed class ModpackRepository(TcMineDbContext db) : IModpackRepository
{
    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct)
    {
        return db.Modpacks.AnyAsync(m => m.Slug == slug, ct);
    }

    public Task<Modpack?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return db.Modpacks
            .Include(m => m.Versions)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<IReadOnlyList<Modpack>> ListAsync(CancellationToken ct)
    {
        return await db.Modpacks
            // AsNoTracking porque é leitura pura: o EF não precisa manter
            // essas entidades sob observação para detectar mudanças, e isso
            // economiza memória e tempo numa listagem.
            .AsNoTracking()
            .OrderBy(m => m.Name)
            .ToListAsync(ct);
    }

    public void Add(Modpack modpack)
    {
        db.Modpacks.Add(modpack);
    }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        return db.SaveChangesAsync(ct);
    }

    public Task<ModpackVersion?> GetVersionAsync(Guid versionId, CancellationToken ct)
    {
        return db.ModpackVersions
            .Include(v => v.Files)
            .FirstOrDefaultAsync(v => v.Id == versionId, ct);
    }
}