using Microsoft.EntityFrameworkCore;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Infrastructure.Persistence;

public sealed class ServerRepository(IDbContextFactory<TcMineDbContext> factory) : IServerRepository
{
    public async Task<IReadOnlyList<GameServer>> ListByModpackAsync(Guid modpackId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.GameServers.AsNoTracking()
            .Where(s => s.ModpackId == modpackId)
            .OrderByDescending(s => s.Id) // GUID v7 = cronológico
            .ToListAsync(ct);
    }

    public async Task<GameServer?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.GameServers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task AddAsync(GameServer server, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.GameServers.Add(server);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(GameServer server, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.GameServers.Update(server);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.GameServers.Where(s => s.Id == id).ExecuteDeleteAsync(ct);
    }
}