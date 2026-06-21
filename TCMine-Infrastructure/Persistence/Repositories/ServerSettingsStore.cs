using Microsoft.EntityFrameworkCore;
using TCMine_Application.Abstractions;
using TCMine_Domain.Entities;

namespace TCMine_Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementação EF Core de <see cref="IServerSettingsStore"/>. Scoped — usa o
/// <see cref="AppDbContext"/> da requisição. Toca apenas a linha única de settings; a cifra dos
/// segredos e o cache ficam no <c>ServerSettingsService</c>.
/// </summary>
public sealed class ServerSettingsStore(AppDbContext db) : IServerSettingsStore
{
    public Task<ServerSettingEntity?> GetAsync(bool tracking, CancellationToken ct = default)
    {
        var query = db.Settings.AsQueryable();
        if (!tracking) query = query.AsNoTracking();
        return query.FirstOrDefaultAsync(s => s.Id == ServerSettingEntity.SingletonId, ct);
    }

    public void Add(ServerSettingEntity row)
    {
        db.Settings.Add(row);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return db.SaveChangesAsync(ct);
    }
}
