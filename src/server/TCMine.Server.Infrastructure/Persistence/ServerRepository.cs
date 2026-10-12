using Microsoft.EntityFrameworkCore;
using TCMine.Contracts.Hubs;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Infrastructure.Persistence;

public sealed class ServerRepository(
    IDbContextFactory<TcMineDbContext> factory,
    IResourceChanges changes) : IServerRepository
{
    public async Task<IReadOnlyList<GameServer>> ListAllAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.GameServers.AsNoTracking()
            .OrderByDescending(s => s.Id) // GUID v7 = cronológico
            .ToListAsync(ct);
    }

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

        changes.Touch(ResourceKind.Servers, server.Id);
    }

    public async Task UpdateAsync(GameServer server, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.GameServers.Update(server);
        await db.SaveChangesAsync(ct);

        // Inclui as mudanças de estado (ligou, parou, caiu): é por aqui que o
        // "no ar" chega ao launcher sem ele ter de perguntar.
        changes.Touch(ResourceKind.Servers, server.Id);
    }

    public async Task UpdateStatusAsync(Guid id, GameServerStatus status, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // Uma coluna, sem materializar a linha: ver o porquê na interface.
        await db.GameServers
            .Where(s => s.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, status), ct);

        changes.Touch(ResourceKind.Servers, id);
    }

    public async Task RemoveAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.GameServers.Where(s => s.Id == id).ExecuteDeleteAsync(ct);

        changes.Touch(ResourceKind.Servers, id);
    }

    public async Task<IReadOnlyList<WorldBackup>> ListBackupsAsync(Guid gameServerId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // Ordena por Id: GUID v7 é cronológico, e o SQLite rejeita
        // DateTimeOffset em ORDER BY.
        return await db.WorldBackups
            .AsNoTracking()
            .Where(b => b.GameServerId == gameServerId)
            .OrderByDescending(b => b.Id)
            .ToListAsync(ct);
    }

    public async Task<(int Count, long TotalBytes)> GetBackupUsageAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // Uma consulta agregada no banco em vez de N idas (uma por servidor):
        // GroupBy(constante) força um único grupo com todas as linhas, e o
        // Count/Sum saem no mesmo SELECT.
        var linha = await db.WorldBackups
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Bytes = g.Sum(b => b.SizeBytes) })
            .FirstOrDefaultAsync(ct);

        return linha is null ? (0, 0L) : (linha.Count, linha.Bytes);
    }

    public async Task<WorldBackup?> GetBackupAsync(Guid backupId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.WorldBackups.AsNoTracking().FirstOrDefaultAsync(b => b.Id == backupId, ct);
    }

    public async Task AddBackupAsync(WorldBackup backup, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.WorldBackups.Add(backup);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveBackupAsync(Guid backupId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.WorldBackups.Where(b => b.Id == backupId).ExecuteDeleteAsync(ct);
    }
}
