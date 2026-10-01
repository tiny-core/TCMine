using Microsoft.EntityFrameworkCore;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Security;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Infrastructure.Persistence;

public sealed class ModpackMembershipRepository(IDbContextFactory<TcMineDbContext> factory)
    : IModpackMembershipRepository
{
    public async Task AddAsync(ModpackMembership membership, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.ModpackMemberships.Add(membership);
        await db.SaveChangesAsync(ct);
    }

    public async Task<ModpackMembership?> GetAsync(Guid userId, Guid modpackId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.ModpackMemberships
            .FirstOrDefaultAsync(m => m.UserId == userId && m.ModpackId == modpackId, ct);
    }

    public async Task<IReadOnlyList<ModpackMemberView>> ListWithUsersAsync(Guid modpackId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // Join explícito, mesma razão do ServerRepository equivalente: não há
        // navegação entre ModpackMembership e User no modelo.
        var lines = await (
            from m in db.ModpackMemberships.AsNoTracking()
            join u in db.Users.AsNoTracking() on m.UserId equals u.Id
            where m.ModpackId == modpackId
            select new { MembershipId = m.Id, m.Role, m.UserId, u.DisplayName }).ToListAsync(ct);

        // Tradução e ordenação fora do banco: ToDto é um switch que não vira
        // SQL, e o papel está gravado como STRING — ordenar por ele no banco
        // daria ordem alfabética (Editor, Owner), não hierarquia.
        return
        [
            .. lines
                .Select(l => new ModpackMemberView(l.MembershipId, l.UserId, l.DisplayName, l.Role.ToDto()))
                .OrderByDescending(v => v.Role)
                .ThenBy(v => v.DisplayName, StringComparer.OrdinalIgnoreCase)
        ];
    }

    public async Task<IReadOnlyList<ModpackMembership>> ListByUserAsync(Guid userId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.ModpackMemberships
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .ToListAsync(ct);
    }

    public async Task<ModpackMemberView?> GetOwnerAsync(Guid modpackId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var line = await (
            from m in db.ModpackMemberships.AsNoTracking()
            join u in db.Users.AsNoTracking() on m.UserId equals u.Id
            where m.ModpackId == modpackId && m.Role == ModpackRole.Owner
            select new { MembershipId = m.Id, m.UserId, u.DisplayName }).FirstOrDefaultAsync(ct);

        return line is null
            ? null
            : new ModpackMemberView(line.MembershipId, line.UserId, line.DisplayName, ModpackRoleDto.Owner);
    }

    public async Task UpdateAsync(ModpackMembership membership, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.ModpackMemberships.Update(membership);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.ModpackMemberships.Where(m => m.Id == id).ExecuteDeleteAsync(ct);
    }
}
