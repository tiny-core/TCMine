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

        // Colunas com escritor próprio ficam de fora. Quem grava a linha
        // inteira (o formulário) leu o servidor antes, e a cópia dele pode
        // estar atrás: sem isto, salvar o formulário enquanto o servidor sobe
        // devolveria o status antigo, apagaria o container recém-criado, ou
        // desfaria a marca de mundo — e com ela o backup antes da troca.
        var entry = db.Entry(server);
        entry.Property(s => s.Status).IsModified = false;
        entry.Property(s => s.ContainerId).IsModified = false;
        entry.Property(s => s.ModpackVersionId).IsModified = false;
        entry.Property(s => s.CloudVaultId).IsModified = false;
        entry.Property(s => s.WorldInitializedAt).IsModified = false;

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

    // Os três abaixo gravam uma coluna (mais o UpdatedAt) sem materializar a
    // linha, pelo mesmo motivo do UpdateStatusAsync: quem chama leu o servidor
    // um instante antes, e gravar a cópia inteira desfaria o que outro fluxo
    // gravou nesse meio tempo.

    public async Task SetContainerIdAsync(Guid id, string? containerId, CancellationToken ct)
    {
        DateTimeOffset? now = DateTimeOffset.UtcNow;

        await using var db = await factory.CreateDbContextAsync(ct);
        await db.GameServers
            .Where(s => s.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.ContainerId, containerId)
                .SetProperty(x => x.UpdatedAt, now), ct);
    }

    public async Task SetPinnedVersionAsync(Guid id, Guid modpackVersionId, CancellationToken ct)
    {
        DateTimeOffset? now = DateTimeOffset.UtcNow;

        await using var db = await factory.CreateDbContextAsync(ct);
        await db.GameServers
            .Where(s => s.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.ModpackVersionId, modpackVersionId)
                .SetProperty(x => x.UpdatedAt, now), ct);

        // A instância de servidor do launcher segue a versão fixada (4E.1).
        changes.Touch(ResourceKind.Servers, id);
    }

    public async Task SetCloudVaultAsync(Guid id, Guid? cloudVaultId, CancellationToken ct)
    {
        DateTimeOffset? now = DateTimeOffset.UtcNow;

        await using var db = await factory.CreateDbContextAsync(ct);
        await db.GameServers
            .Where(s => s.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.CloudVaultId, cloudVaultId)
                .SetProperty(x => x.UpdatedAt, now), ct);
    }

    public async Task<bool> MarkWorldInitializedAsync(Guid id, DateTimeOffset at, CancellationToken ct)
    {
        // Anulável como a coluna, pelo mesmo motivo do CloudCredentialRepository:
        // sem isto a inferência do SetProperty fica entre os dois tipos.
        DateTimeOffset? value = at;

        await using var db = await factory.CreateDbContextAsync(ct);

        // O filtro "ainda vazia" é o que torna a chamada repetível: o segundo a
        // chegar atualiza zero linhas e a primeira data fica.
        var rows = await db.GameServers
            .Where(s => s.Id == id && s.WorldInitializedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.WorldInitializedAt, value), ct);

        return rows > 0;
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
