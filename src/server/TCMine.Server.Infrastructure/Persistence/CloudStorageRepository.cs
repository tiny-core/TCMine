using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Cloud;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Infrastructure.Persistence;

/// <summary>
///     Persistência do protocolo da nuvem. A garantia central mora em
///     <see cref="CommitAppliedAsync" />: um <c>SaveChanges</c> só (transação
///     implícita do EF) com o lease conferido pela versão LIDA — se outra
///     requisição mexeu no lease no meio, nada é gravado.
/// </summary>
public sealed class CloudStorageRepository(IDbContextFactory<TcMineDbContext> factory) : ICloudStorageRepository
{
    public async Task<CloudLease?> GetLeaseAsync(Guid vaultId, string playerUuid, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CloudLeases.AsNoTracking()
            .FirstOrDefaultAsync(l => l.VaultId == vaultId && l.PlayerUuid == playerUuid, ct);
    }

    public async Task<bool> SaveLeaseAsync(CloudLease lease, long? expectedVersion, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (expectedVersion is null)
            db.CloudLeases.Add(lease);
        else
            AttachLease(db, lease, expectedVersion.Value);

        return await TrySaveAsync(db, ct);
    }

    public async Task<IReadOnlyList<(CloudLease Lease, TimeSpan Ttl)>> ListHeldLeasesAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await (
            from l in db.CloudLeases.AsNoTracking()
            join v in db.CloudVaults.AsNoTracking() on l.VaultId equals v.Id
            where l.HolderServerId != null
            select new { Lease = l, v.LeaseTtlMinutes }
        ).ToListAsync(ct);
        return [.. rows.Select(r => (r.Lease, TimeSpan.FromMinutes(r.LeaseTtlMinutes)))];
    }

    public async Task<IReadOnlyList<CloudChannel>> ListChannelsAsync(Guid vaultId, string playerUuid,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var channels = await db.CloudChannels.AsNoTracking()
            .Where(c => c.VaultId == vaultId && c.PlayerUuid == playerUuid)
            .ToListAsync(ct);
        // O mais antigo primeiro: é o canal padrão do mod. Pela data de criação, em
        // memória (o SQLite não ordena DateTimeOffset), com o Id só de desempate —
        // o GUID v7 do .NET NÃO é ordenado dentro do mesmo milissegundo.
        return [.. channels.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)];
    }

    public async Task AddChannelAsync(CloudChannel channel, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.CloudChannels.Add(channel);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CloudSnapshotItem>> LoadSnapshotAsync(IReadOnlyCollection<Guid> channelIds,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await (
            from b in db.CloudBalances.AsNoTracking()
            join i in db.CloudItemTypes.AsNoTracking() on b.ItemTypeId equals i.Id
            where channelIds.Contains(b.ChannelId) && b.Amount > 0
            select new { b.ChannelId, Item = i, b.Amount }
        ).ToListAsync(ct);
        return [.. rows.Select(r => new CloudSnapshotItem(r.ChannelId, r.Item, r.Amount))];
    }

    public async Task<IReadOnlyList<CloudBalance>> ListBalancesAsync(IReadOnlyCollection<Guid> channelIds,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CloudBalances.AsNoTracking().Where(b => channelIds.Contains(b.ChannelId)).ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<string, Guid>> ItemIdsByFingerprintAsync(
        IReadOnlyCollection<string> fingerprints, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CloudItemTypes.AsNoTracking()
            .Where(i => fingerprints.Contains(i.Fingerprint))
            .ToDictionaryAsync(i => i.Fingerprint, i => i.Id, StringComparer.Ordinal, ct);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> FingerprintsByIdAsync(IReadOnlyCollection<Guid> ids,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CloudItemTypes.AsNoTracking()
            .Where(i => ids.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Fingerprint, ct);
    }

    public async Task<CloudBatch?> FindBatchAsync(Guid vaultId, string playerUuid, long epoch, long seq,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CloudBatches.AsNoTracking().FirstOrDefaultAsync(
            b => b.VaultId == vaultId && b.PlayerUuid == playerUuid && b.Epoch == epoch && b.Seq == seq, ct);
    }

    public async Task<bool> CommitAppliedAsync(CloudLease lease, long expectedLeaseVersion, CloudBatch batch,
        IReadOnlyList<CloudItemType> newItemTypes, IReadOnlyList<CloudBalanceChange> changes, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        AttachLease(db, lease, expectedLeaseVersion);
        db.CloudItemTypes.AddRange(newItemTypes);
        db.CloudBatches.Add(batch);

        var channelIds = changes.Select(c => c.ChannelId).Distinct().ToArray();
        var itemIds = changes.Select(c => c.ItemTypeId).Distinct().ToArray();
        var existing = await db.CloudBalances
            .Where(b => channelIds.Contains(b.ChannelId) && itemIds.Contains(b.ItemTypeId))
            .ToDictionaryAsync(b => (b.ChannelId, b.ItemTypeId), ct);

        foreach (var change in changes)
        {
            if (!existing.TryGetValue((change.ChannelId, change.ItemTypeId), out var balance))
            {
                balance = new CloudBalance { ChannelId = change.ChannelId, ItemTypeId = change.ItemTypeId };
                db.CloudBalances.Add(balance);
            }

            // O saldo relido aqui tem de dar o MESMO resultado que a decisão
            // calculou. Se não der, alguém mudou o saldo por fora do lease (bug):
            // aborta tudo em vez de gravar um ledger que não fecha.
            if (balance.Apply(change.Delta) != change.BalanceAfter)
            {
                throw new InvalidOperationException(
                    $"Saldo do canal {change.ChannelId} mudou fora do lease; lote {batch.Epoch}/{batch.Seq} abortado.");
            }

            db.CloudLedger.Add(new CloudLedgerEntry
            {
                ChannelId = change.ChannelId,
                ItemTypeId = change.ItemTypeId,
                Delta = change.Delta,
                BalanceAfter = change.BalanceAfter,
                Source = CloudLedgerSource.Game,
                BatchId = batch.Id
            });
        }

        return await TrySaveAsync(db, ct);
    }

    public async Task<bool> CommitAdminAsync(CloudAdminCommit commit, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        foreach (var (lease, read) in commit.Leases)
        {
            if (read is null)
                db.CloudLeases.Add(lease);
            else
                AttachLease(db, lease, read.Value);
        }

        db.CloudItemTypes.AddRange(commit.NewItemTypes);
        foreach (var entity in commit.AlsoUpdate)
            db.Update(entity);

        var channelIds = commit.Changes.Select(c => c.ChannelId).Distinct().ToArray();
        var itemIds = commit.Changes.Select(c => c.ItemTypeId).Distinct().ToArray();
        var existing = await db.CloudBalances
            .Where(b => channelIds.Contains(b.ChannelId) && itemIds.Contains(b.ItemTypeId))
            .ToDictionaryAsync(b => (b.ChannelId, b.ItemTypeId), ct);
        foreach (var change in commit.Changes)
        {
            if (!existing.TryGetValue((change.ChannelId, change.ItemTypeId), out var balance))
            {
                balance = new CloudBalance { ChannelId = change.ChannelId, ItemTypeId = change.ItemTypeId };
                db.CloudBalances.Add(balance);
                existing[(change.ChannelId, change.ItemTypeId)] = balance;
            }

            if (balance.Apply(change.Delta) != change.BalanceAfter)
            {
                throw new InvalidOperationException(
                    $"Saldo do canal {change.ChannelId} mudou durante a decisão do painel.");
            }

            db.CloudLedger.Add(new CloudLedgerEntry
            {
                ChannelId = change.ChannelId,
                ItemTypeId = change.ItemTypeId,
                Delta = change.Delta,
                BalanceAfter = change.BalanceAfter,
                Source = commit.Source,
                BatchId = change.BatchId,
                ActorUserId = commit.ActorUserId,
                Reason = commit.Reason.Length > 512 ? commit.Reason[..512] : commit.Reason
            });
        }

        return await TrySaveAsync(db, ct);
    }

    public async Task<IReadOnlyList<CloudBatch>> ListAppliedBatchesAsync(Guid vaultId, Guid serverId,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CloudBatches.AsNoTracking()
            .Where(b => b.VaultId == vaultId && b.ServerId == serverId && b.Status == CloudBatchStatus.Applied)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CloudLedgerEntry>> ListLedgerByBatchesAsync(IReadOnlyCollection<Guid> batchIds,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CloudLedger.AsNoTracking()
            .Where(e => e.BatchId != null && batchIds.Contains(e.BatchId.Value))
            .ToListAsync(ct);
    }

    public async Task CommitQuarantineAsync(CloudBatch batch, CloudQuarantine quarantine,
        IReadOnlyCollection<Guid> channelsToFreeze, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.CloudBatches.Add(batch);
        db.CloudQuarantine.Add(quarantine);
        foreach (var channel in await db.CloudChannels.Where(c => channelsToFreeze.Contains(c.Id)).ToListAsync(ct))
            channel.Freeze($"Lote {batch.Epoch}/{batch.Seq} em quarentena: {quarantine.Reason}");

        if (!await TrySaveAsync(db, ct))
            throw new CloudConcurrencyException($"Lote {batch.Epoch}/{batch.Seq} gravado por outra requisição.");
    }

    /// <summary>
    ///     Anexa o lease como modificado, com a versão ORIGINAL = a lida. O EF põe
    ///     "WHERE Version = lida" no UPDATE; sem isto ele usaria a versão já
    ///     incrementada pelo domínio e o UPDATE nunca bateria com a linha.
    /// </summary>
    private static void AttachLease(TcMineDbContext db, CloudLease lease, long expectedVersion)
    {
        var entry = db.CloudLeases.Update(lease);
        entry.Property(l => l.Version).OriginalValue = expectedVersion;
    }

    /// <summary>
    ///     Concorrência (versão do lease) e índice único (lote, item, lease novo
    ///     criados ao mesmo tempo) são a MESMA situação para quem chama: perdeu a
    ///     corrida, nada foi gravado, tenta de novo. Qualquer OUTRA falha (chave
    ///     estrangeira, coluna estourada) é bug e sobe: tratá-la como conflito
    ///     faria o servidor de jogo reenviar para sempre um lote que nunca passa.
    /// </summary>
    private static async Task<bool> TrySaveAsync(TcMineDbContext db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
        catch (DbUpdateException e) when (IsUniqueViolation(e))
        {
            return false;
        }
    }

    /// <summary>
    ///     A Infrastructure não referencia os pacotes de cada banco, então a
    ///     checagem vai pelo DbException genérico: SQLSTATE 23505 no Postgres e a
    ///     mensagem do SQLite (o código 19 dele cobre qualquer restrição, inclusive
    ///     chave estrangeira, e por isso não serve sozinho).
    /// </summary>
    private static bool IsUniqueViolation(DbUpdateException e) =>
        e.InnerException is DbException db
        && (db.SqlState == "23505" || db.Message.Contains("UNIQUE constraint failed", StringComparison.Ordinal));
}
