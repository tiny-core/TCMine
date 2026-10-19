using Microsoft.EntityFrameworkCore;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Cloud;
using TCMine.Server.Domain.Common;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Infrastructure.Persistence;

/// <summary>
///     O apagar da retenção (as regras estão em <c>RunRetention</c>).
///     Tudo corta pelo <c>Id</c> (GUID v7, ver <see cref="IdTime" />): o SQLite não
///     compara <c>DateTimeOffset</c>, e o Id já está em todo índice. Apaga em
///     blocos — escolhe os ids do bloco e apaga só eles — para cada transação
///     ser curta e não segurar a tabela que o jogo está usando.
/// </summary>
public sealed class RetentionRepository(IDbContextFactory<TcMineDbContext> factory) : IRetentionRepository
{
    /// <summary>Linhas por bloco: grande o bastante para andar rápido, pequeno para não travar ninguém.</summary>
    public const int BlockSize = 5000;

    public async Task<IReadOnlyList<CloudVaultRetention>> ListVaultRetentionAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CloudVaults.AsNoTracking()
            .Select(v => new CloudVaultRetention(v.Id, v.GameHistoryDays))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CloudServerHistory>> ListServersWithGameHistoryAsync(Guid vaultId,
        DateTimeOffset before, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var floor = IdTime.Floor(before);
        var servers = await db.CloudBatches.AsNoTracking()
            .Where(b => b.VaultId == vaultId && b.Id.CompareTo(floor) < 0)
            .Select(b => b.ServerId)
            .Distinct()
            .ToListAsync(ct);
        if (servers.Count == 0)
            return [];

        var withOpenIncident = (await db.CloudRollbackIncidents.AsNoTracking()
                .Where(i => servers.Contains(i.ServerId) && i.Status == CloudIncidentStatus.Open)
                .Select(i => i.ServerId)
                .ToListAsync(ct))
            .ToHashSet();

        // O mais antigo em memória: são poucos backups por servidor, e o SQLite
        // não sabe comparar DateTimeOffset numa agregação.
        var oldestBackup = (await db.WorldBackups.AsNoTracking()
                .Where(w => servers.Contains(w.GameServerId))
                .Select(w => new { w.GameServerId, w.CreatedAt })
                .ToListAsync(ct))
            .GroupBy(w => w.GameServerId)
            .ToDictionary(g => g.Key, g => g.Min(w => w.CreatedAt));

        return
        [
            .. servers.Select(s => new CloudServerHistory(s, withOpenIncident.Contains(s),
                oldestBackup.TryGetValue(s, out var at) ? at : null))
        ];
    }

    public async Task<GameHistoryPurged> PurgeServerGameHistoryAsync(Guid vaultId, Guid serverId,
        DateTimeOffset before, CancellationToken ct)
    {
        var floor = IdTime.Floor(before);

        // Primeiro o ledger de origem JOGO: a chave estrangeira do ledger para o
        // lote impede apagar um lote enquanto houver linha apontando para ele.
        var ledger = await DeleteInBlocksAsync(db =>
        {
            var serverBatches = db.CloudBatches
                .Where(b => b.VaultId == vaultId && b.ServerId == serverId)
                .Select(b => b.Id);
            return db.CloudLedger
                .Where(e => e.VaultId == vaultId && e.Source == CloudLedgerSource.Game
                            && e.Id.CompareTo(floor) < 0
                            && e.BatchId != null && serverBatches.Contains(e.BatchId.Value))
                .OrderBy(e => e.Id)
                .Select(e => e.Id);
        }, (db, ids) => db.CloudLedger.Where(e => ids.Contains(e.Id)).ExecuteDeleteAsync(ct), ct);

        // Depois os lotes que ficaram sem ninguém apontando. Os que uma decisão
        // do dono referencia (linha de quarentena aplicada, a quarentena em si)
        // ficam: esse histórico não expira. Estornados também ficam — são parte
        // de um incidente, que é decisão do dono.
        var batches = await DeleteInBlocksAsync(db => db.CloudBatches
                .Where(b => b.VaultId == vaultId && b.ServerId == serverId && b.Id.CompareTo(floor) < 0
                            && (b.Status == CloudBatchStatus.Applied || b.Status == CloudBatchStatus.Discarded)
                            && !db.CloudLedger.Any(e => e.BatchId == b.Id)
                            && !db.CloudQuarantine.Any(q => q.BatchId == b.Id))
                .OrderBy(b => b.Id)
                .Select(b => b.Id),
            (db, ids) => db.CloudBatches.Where(b => ids.Contains(b.Id)).ExecuteDeleteAsync(ct), ct);

        return new GameHistoryPurged(ledger, batches);
    }

    public Task<int> PurgeActivityAsync(DateTimeOffset before, CancellationToken ct)
    {
        var floor = IdTime.Floor(before);
        return DeleteInBlocksAsync(
            db => db.ActivityEvents.Where(a => a.Id.CompareTo(floor) < 0).OrderBy(a => a.Id).Select(a => a.Id),
            (db, ids) => db.ActivityEvents.Where(a => ids.Contains(a.Id)).ExecuteDeleteAsync(ct), ct);
    }

    public async Task<HousekeepingPurged> PurgeHousekeepingAsync(DateTimeOffset before, DateTimeOffset now,
        CancellationToken ct)
    {
        var floor = IdTime.Floor(before);
        await using var db = await factory.CreateDbContextAsync(ct);

        // Poucas linhas: a decisão ("ainda vale?", "resolvido há quanto?") é das
        // entidades e corre em memória — o SQLite não compararia as datas.
        var deadInvites = (await db.Invites.AsNoTracking()
                .Where(i => i.Id.CompareTo(floor) < 0)
                .ToListAsync(ct))
            .Where(i => !i.IsUsable(now))
            .Select(i => i.Id)
            .ToList();
        var resolvedRequests = (await db.AccessRequests.AsNoTracking()
                .Where(r => r.Id.CompareTo(floor) < 0 && r.Status != AccessRequestStatus.Pending)
                .ToListAsync(ct))
            .Where(r => r.ResolvedAt is { } at && at < before)
            .Select(r => r.Id)
            .ToList();

        var invites = 0;
        foreach (var block in deadInvites.Chunk(BlockSize))
            invites += await db.Invites.Where(i => block.Contains(i.Id)).ExecuteDeleteAsync(ct);
        var requests = 0;
        foreach (var block in resolvedRequests.Chunk(BlockSize))
            requests += await db.AccessRequests.Where(r => block.Contains(r.Id)).ExecuteDeleteAsync(ct);

        return new HousekeepingPurged(invites, requests);
    }

    /// <summary>
    ///     Repete "escolhe até <see cref="BlockSize" /> ids; apaga esses" até não
    ///     sobrar nada. Cada bloco num contexto novo: o anterior não acumula nada,
    ///     e cada DELETE é a sua própria transação curta.
    /// </summary>
    private async Task<int> DeleteInBlocksAsync(Func<TcMineDbContext, IQueryable<Guid>> pick,
        Func<TcMineDbContext, List<Guid>, Task<int>> delete, CancellationToken ct)
    {
        var total = 0;
        while (true)
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var ids = await pick(db).Take(BlockSize).ToListAsync(ct);
            if (ids.Count == 0)
                return total;

            total += await delete(db, ids);
            if (ids.Count < BlockSize)
                return total;
        }
    }
}
