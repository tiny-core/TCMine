using Microsoft.EntityFrameworkCore;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Infrastructure.Persistence;

/// <inheritdoc cref="IImportRequestRepository" />
public sealed class ImportRequestRepository(IDbContextFactory<TcMineDbContext> factory) : IImportRequestRepository
{
    public async Task AddAsync(ImportRequest request, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.ImportRequests.Add(request);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ImportRequest request, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.ImportRequests.Update(request);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Guid requestId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // ExecuteDelete em vez de carregar e remover: é uma ida ao banco em vez
        // de duas, e some com a corrida entre ler e apagar.
        await db.ImportRequests
            .Where(r => r.Id == requestId)
            .ExecuteDeleteAsync(ct);
    }

    public async Task<IReadOnlyList<ImportRequest>> ListAllAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.ImportRequests
            // Guid v7 e cronologico: retoma na ordem em que foi pedido.
            .OrderBy(r => r.Id)
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsForAsync(ModFileOrigin origin, string projectId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.ImportRequests
            .AsNoTracking()
            .AnyAsync(r => r.Origin == origin && r.ProjectId == projectId, ct);
    }
}
