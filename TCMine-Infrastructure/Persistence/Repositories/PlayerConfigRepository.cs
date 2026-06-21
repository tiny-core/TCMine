using Microsoft.EntityFrameworkCore;
using TCMine_Application.Abstractions;
using TCMine_Domain.Entities;

namespace TCMine_Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementação EF Core de <see cref="IPlayerConfigRepository"/>. Scoped — usa o
/// <see cref="AppDbContext"/> da requisição. Upsert last-write-wins por <c>(uuid, modpackId)</c>.
/// </summary>
public sealed class PlayerConfigRepository(AppDbContext db) : IPlayerConfigRepository
{
    public Task<PlayerConfigEntity?> GetAsync(string uuid, string modpackId, CancellationToken ct = default)
    {
        return db.PlayerConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Uuid == uuid && p.ModpackId == modpackId, ct);
    }

    public async Task<DateTime> UpsertAsync(string uuid, string modpackId, byte[] zip, CancellationToken ct = default)
    {
        var existing = await db.PlayerConfigs
            .FirstOrDefaultAsync(p => p.Uuid == uuid && p.ModpackId == modpackId, ct);

        var now = DateTime.UtcNow;
        if (existing is null)
        {
            db.PlayerConfigs.Add(new PlayerConfigEntity
            {
                Uuid = uuid, ModpackId = modpackId, UpdatedAt = now
            });
        }
        else
        {
            existing.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return now;
    }
}
