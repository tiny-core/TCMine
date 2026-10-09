using Microsoft.EntityFrameworkCore;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Infrastructure.Persistence;

public sealed class AccessRequestRepository(IDbContextFactory<TcMineDbContext> factory) : IAccessRequestRepository
{
    public async Task AddAsync(AccessRequest request, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.AccessRequests.Add(request);
        await db.SaveChangesAsync(ct);
    }

    public async Task<AccessRequest?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.AccessRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<AccessRequest?> GetPendingAsync(Guid userId, Guid gameServerId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.AccessRequests.FirstOrDefaultAsync(
            r => r.UserId == userId && r.GameServerId == gameServerId && r.Status == AccessRequestStatus.Pending,
            ct);
    }

    public async Task<IReadOnlyList<AccessRequest>> ListPendingByUserAsync(Guid userId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.AccessRequests
            .AsNoTracking()
            .Where(r => r.UserId == userId && r.Status == AccessRequestStatus.Pending)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AccessRequestView>> ListPendingForServersAsync(
        IReadOnlyList<Guid> gameServerIds, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // Join explícito, como em MembershipRepository.ListWithUsersAsync: não
        // há navegação entre AccessRequest, User e GameServer no modelo.
        var linhas = await (
            from r in db.AccessRequests.AsNoTracking()
            join u in db.Users.AsNoTracking() on r.UserId equals u.Id
            join s in db.GameServers.AsNoTracking() on r.GameServerId equals s.Id
            where gameServerIds.Contains(r.GameServerId) && r.Status == AccessRequestStatus.Pending
            select new
            {
                r.Id,
                r.UserId,
                u.DisplayName,
                r.GameServerId,
                ServerName = s.Name,
                r.CreatedAt
            }
        ).ToListAsync(ct);

        // Ordem por Id (GUID v7, cronológico): o SQLite rejeita DateTimeOffset
        // em ORDER BY, e o pedido mais antigo é o que o admin deveria ver primeiro.
        return
        [
            .. linhas
                .OrderBy(l => l.Id)
                .Select(l =>
                    new AccessRequestView(l.Id, l.UserId, l.DisplayName, l.GameServerId, l.ServerName, l.CreatedAt))
        ];
    }

    public async Task UpdateAsync(AccessRequest request, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.AccessRequests.Update(request);
        await db.SaveChangesAsync(ct);
    }
}
