using Microsoft.EntityFrameworkCore;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Security;
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

    public async Task<IReadOnlyList<ServerMemberView>> ListWithUsersAsync(
        Guid gameServerId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // Join explícito: não há navegação entre Membership e User no modelo, e
        // criá-la só para esta tela arrastaria carregamento implícito para todas
        // as checagens de permissão, que não querem o usuário inteiro.
        var linhas = await (
            from m in db.Memberships.AsNoTracking()
            join u in db.Users.AsNoTracking() on m.UserId equals u.Id
            where m.GameServerId == gameServerId
            select new
            {
                MembershipId = m.Id,
                m.Role,
                m.UserId,
                u.DisplayName,
                u.MinecraftUuid,
                u.LastSeenAt
            }).ToListAsync(ct);

        // Tradução e ordenação fora do banco de propósito: ToDto é um switch que
        // não vira SQL, e o papel está gravado como STRING — ordenar por ele no
        // banco daria ordem alfabética (Admin, Member, Moderator), não hierarquia.
        return
        [
            .. linhas
                .Select(l => new ServerMemberView(
                    l.MembershipId, l.UserId, l.DisplayName, l.MinecraftUuid,
                    l.Role.ToDto(), l.LastSeenAt))
                .OrderByDescending(v => v.Role)
                .ThenBy(v => v.DisplayName, StringComparer.OrdinalIgnoreCase)
        ];
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
