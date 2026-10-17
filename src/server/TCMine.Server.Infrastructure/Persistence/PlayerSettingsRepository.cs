using Microsoft.EntityFrameworkCore;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Infrastructure.Persistence;

public sealed class PlayerSettingsRepository(IDbContextFactory<TcMineDbContext> factory) : IPlayerSettingsRepository
{
    public async Task<PlayerModpackSettings?> GetAsync(Guid userId, Guid modpackId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.PlayerModpackSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.ModpackId == modpackId, ct);
    }

    public async Task AddAsync(PlayerModpackSettings settings, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.PlayerModpackSettings.Add(settings);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(PlayerModpackSettings settings, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.PlayerModpackSettings.Update(settings);
        await db.SaveChangesAsync(ct);
    }
}
