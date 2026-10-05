using Microsoft.EntityFrameworkCore;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Infrastructure.Persistence;

public sealed class CloudGovernanceRepository(IDbContextFactory<TcMineDbContext> factory) : ICloudGovernanceRepository
{
    // ---------------------------------------------------------------- regras

    public async Task<IReadOnlyList<CloudItemRule>> ListRulesAsync(Guid vaultId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var rules = await db.CloudItemRules.AsNoTracking().Where(r => r.VaultId == vaultId).ToListAsync(ct);
        return [.. rules.OrderBy(r => r.Scope).ThenBy(r => r.Pattern, StringComparer.Ordinal)];
    }

    public async Task<CloudItemRule?> GetRuleAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CloudItemRules.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<bool> AddRuleAsync(CloudItemRule rule, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (await db.CloudItemRules.AnyAsync(
                r => r.VaultId == rule.VaultId && r.Scope == rule.Scope && r.Pattern == rule.Pattern, ct))
            return false;
        db.CloudItemRules.Add(rule);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task RemoveRuleAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.CloudItemRules.Where(r => r.Id == id).ExecuteDeleteAsync(ct);
    }

    // ---------------------------------------------------------------- suspeitos

    public async Task RecordSuspectsAsync(Guid vaultId, IReadOnlyList<(string ItemId, string Evidence, long Attempts)> items,
        DateTimeOffset now, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var ids = items.Select(i => i.ItemId).Distinct().ToArray();
        var existing = await db.CloudSuspectItems
            .Where(s => s.VaultId == vaultId && ids.Contains(s.ItemId))
            .ToDictionaryAsync(s => s.ItemId, ct);
        foreach (var (itemId, evidence, attempts) in items)
        {
            if (!existing.TryGetValue(itemId, out var suspect))
            {
                suspect = new CloudSuspectItem { VaultId = vaultId, ItemId = itemId };
                db.CloudSuspectItems.Add(suspect);
                existing[itemId] = suspect;
            }
            suspect.Seen(attempts, evidence, now);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CloudSuspectItem>> ListSuspectsAsync(Guid vaultId, CloudSuspectStatus? status,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var list = await db.CloudSuspectItems.AsNoTracking()
            .Where(s => s.VaultId == vaultId && (status == null || s.Status == status))
            .ToListAsync(ct);
        return [.. list.OrderByDescending(s => s.Attempts)];
    }

    public async Task<CloudSuspectItem?> GetSuspectAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CloudSuspectItems.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task UpdateSuspectAsync(CloudSuspectItem suspect, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.CloudSuspectItems.Update(suspect);
        await db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- em dúvida

    public async Task AddDoubtfulAsync(IReadOnlyList<CloudDoubtfulOperation> operations, CancellationToken ct)
    {
        if (operations.Count == 0) return;
        await using var db = await factory.CreateDbContextAsync(ct);
        var server = operations[0].ServerId;
        var reports = operations.Select(o => o.ReportId).Distinct().ToArray();
        var known = (await db.CloudDoubtfulOperations.AsNoTracking()
                .Where(d => d.ServerId == server && reports.Contains(d.ReportId))
                .Select(d => new { d.ReportId, d.Index })
                .ToListAsync(ct))
            .Select(k => (k.ReportId, k.Index))
            .ToHashSet();
        db.CloudDoubtfulOperations.AddRange(operations.Where(o => !known.Contains((o.ReportId, o.Index))));
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CloudDoubtfulOperation>> ListDoubtfulAsync(Guid vaultId, bool openOnly,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var list = await db.CloudDoubtfulOperations.AsNoTracking()
            .Where(d => d.VaultId == vaultId && (!openOnly || d.Status == CloudDoubtfulStatus.Open))
            .ToListAsync(ct);
        return [.. list.OrderByDescending(d => d.Id)];
    }

    public async Task<CloudDoubtfulOperation?> GetDoubtfulAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CloudDoubtfulOperations.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    public async Task UpdateDoubtfulAsync(CloudDoubtfulOperation operation, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.CloudDoubtfulOperations.Update(operation);
        await db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- incidentes

    public async Task<CloudRollbackIncident?> GetOpenIncidentAsync(Guid serverId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CloudRollbackIncidents.AsNoTracking()
            .FirstOrDefaultAsync(i => i.ServerId == serverId && i.Status == CloudIncidentStatus.Open, ct);
    }

    public async Task AddIncidentAsync(CloudRollbackIncident incident, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.CloudRollbackIncidents.Add(incident);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CloudRollbackIncident>> ListIncidentsAsync(Guid vaultId, bool openOnly,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var list = await db.CloudRollbackIncidents.AsNoTracking()
            .Where(i => i.VaultId == vaultId && (!openOnly || i.Status == CloudIncidentStatus.Open))
            .ToListAsync(ct);
        return [.. list.OrderByDescending(i => i.Id)];
    }

    public async Task<CloudRollbackIncident?> GetIncidentAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CloudRollbackIncidents.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task UpdateIncidentAsync(CloudRollbackIncident incident, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.CloudRollbackIncidents.Update(incident);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Guid?> PreviousWorldAsync(Guid serverId, Guid currentCredentialId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var credentials = await db.CloudServerCredentials.AsNoTracking()
            .Where(c => c.GameServerId == serverId && c.Id != currentCredentialId && c.WorldId != null)
            .Select(c => new { c.Id, c.CreatedAt, c.WorldId })
            .ToListAsync(ct);
        // Em memória: o SQLite não ordena DateTimeOffset; o Id desempata.
        return credentials.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id).FirstOrDefault()?.WorldId;
    }

    public async Task<IReadOnlyDictionary<string, (long Epoch, long Seq)>> LastAppliedByServerAsync(Guid vaultId,
        Guid serverId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var batches = await db.CloudBatches.AsNoTracking()
            .Where(b => b.VaultId == vaultId && b.ServerId == serverId && b.Status == CloudBatchStatus.Applied)
            .Select(b => new { b.PlayerUuid, b.Epoch, b.Seq })
            .ToListAsync(ct);
        return batches
            .GroupBy(b => b.PlayerUuid)
            .ToDictionary(g => g.Key, g => g.Select(b => (b.Epoch, b.Seq)).Max());
    }

    // ---------------------------------------------------------------- quarentena

    public async Task<IReadOnlyList<CloudQuarantineView>> ListQuarantineAsync(Guid vaultId, bool openOnly,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await (
            from q in db.CloudQuarantine.AsNoTracking()
            join b in db.CloudBatches.AsNoTracking() on q.BatchId equals b.Id
            where q.VaultId == vaultId && (!openOnly || q.ResolvedAt == null)
            select new { q, b }
        ).ToListAsync(ct);
        return [.. rows.OrderByDescending(r => r.q.Id).Select(r => new CloudQuarantineView(r.q, r.b))];
    }

    public async Task<CloudQuarantineView?> GetQuarantineAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await (
            from q in db.CloudQuarantine.AsNoTracking()
            join b in db.CloudBatches.AsNoTracking() on q.BatchId equals b.Id
            where q.Id == id
            select new { q, b }
        ).FirstOrDefaultAsync(ct);
        return row is null ? null : new CloudQuarantineView(row.q, row.b);
    }

    public async Task UpdateQuarantineAsync(CloudQuarantine quarantine, CloudBatch batch, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.CloudQuarantine.Update(quarantine);
        db.CloudBatches.Update(batch);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, (string ItemId, string DisplayName)>> ItemNamesAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return (await db.CloudItemTypes.AsNoTracking()
                .Where(i => ids.Contains(i.Id))
                .Select(i => new { i.Id, i.ItemId, i.DisplayName })
                .ToListAsync(ct))
            .ToDictionary(i => i.Id, i => (i.ItemId, i.DisplayName));
    }

    // ---------------------------------------------------------------- auditoria

    public async Task AddAuditAsync(CloudAdminAuditEntry entry, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.CloudAdminAudit.Add(entry);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CloudAdminAuditEntry>> ListAuditAsync(Guid vaultId, int limit, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CloudAdminAudit.AsNoTracking()
            .Where(a => a.VaultId == vaultId)
            .OrderByDescending(a => a.Id)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CloudLedgerView>> ListLedgerAsync(Guid vaultId, string? playerUuid, int limit,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await (
            from e in db.CloudLedger.AsNoTracking()
            join c in db.CloudChannels.AsNoTracking() on e.ChannelId equals c.Id
            join i in db.CloudItemTypes.AsNoTracking() on e.ItemTypeId equals i.Id
            where c.VaultId == vaultId && (playerUuid == null || c.PlayerUuid == playerUuid)
            orderby e.Id descending
            select new CloudLedgerView(e.Id, e.CreatedAt, c.PlayerUuid, c.Name, i.ItemId, i.DisplayName, e.Delta,
                e.BalanceAfter, e.Source, e.Reason)
        ).Take(limit).ToListAsync(ct);
        return rows;
    }
}
