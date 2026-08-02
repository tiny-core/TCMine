using Microsoft.EntityFrameworkCore;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Infrastructure.Persistence;

public sealed class NewsRepository(IDbContextFactory<TcMineDbContext> factory) : INewsRepository
{
    public async Task<IReadOnlyList<News>> ListByModpackAsync(Guid modpackId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.News.AsNoTracking()
            .Where(n => n.ModpackId == modpackId)
            .OrderByDescending(n => n.Id) // GUID v7 = cronológico (SQLite não ordena DateTimeOffset)
            .ToListAsync(ct);
    }

    public async Task<News?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.News.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id, ct);
    }

    public async Task AddAsync(News news, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.News.Add(news);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(News news, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.News.Update(news);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.News.Where(n => n.Id == id).ExecuteDeleteAsync(ct);
    }
}
