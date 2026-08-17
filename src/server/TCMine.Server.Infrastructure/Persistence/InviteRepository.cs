using Microsoft.EntityFrameworkCore;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Infrastructure.Persistence;

public sealed class InviteRepository(IDbContextFactory<TcMineDbContext> factory) : IInviteRepository
{
    public async Task AddAsync(Invite invite, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Invites.Add(invite);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Invite?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Invites.FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task<Invite?> GetByCodeHashAsync(string codeHash, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Invites.FirstOrDefaultAsync(i => i.CodeHash == codeHash, ct);
    }

    public async Task<IReadOnlyList<Invite>> ListByServerAsync(Guid gameServerId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // Ordena por Id, não por CreatedAt: o GUID v7 já é cronológico, e o
        // SQLite rejeita DateTimeOffset em ORDER BY.
        return await db.Invites
            .AsNoTracking()
            .Where(i => i.GameServerId == gameServerId)
            .OrderByDescending(i => i.Id)
            .ToListAsync(ct);
    }

    public async Task UpdateAsync(Invite invite, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Invites.Update(invite);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class MembershipRepository(IDbContextFactory<TcMineDbContext> factory) : IMembershipRepository
{
    public async Task AddAsync(Membership membership, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Memberships.Add(membership);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Membership?> GetAsync(Guid userId, Guid gameServerId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Memberships
            .FirstOrDefaultAsync(m => m.UserId == userId && m.GameServerId == gameServerId, ct);
    }

    public async Task<IReadOnlyList<Membership>> ListByServerAsync(Guid gameServerId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Memberships
            .AsNoTracking()
            .Where(m => m.GameServerId == gameServerId)
            .OrderBy(m => m.Id)
            .ToListAsync(ct);
    }

    public async Task UpdateAsync(Membership membership, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Memberships.Update(membership);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Memberships.Where(m => m.Id == id).ExecuteDeleteAsync(ct);
    }
}
