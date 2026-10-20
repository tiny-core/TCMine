using Microsoft.EntityFrameworkCore;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Cloud;
using TCMine.Server.Domain.Common;

namespace TCMine.Server.Infrastructure.Persistence;

public sealed class CloudGovernanceRepository(IDbContextFactory<TcMineDbContext> factory) : ICloudGovernanceRepository
{
    // ---------------------------------------------------------------- regras

    public async Task<IReadOnlyList<CloudItemRule>> ListRulesAsync(Guid vaultId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var rules = await db.CloudItemRules.AsNoTracking().Where(r => r.VaultId == vaultId).ToListAsync(ct);

        // Em memória DE PROPÓSITO (fase 6.4): o Scope é gravado como texto, então
        // no banco a ordem seria a alfabética do nome e não a do enum, e a
        // ordenação de texto muda com a collation de cada banco. A política do
        // mod precisa de todas as regras de qualquer forma, e são poucas.
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

    public async Task RecordSuspectsAsync(Guid vaultId,
        IReadOnlyList<(string ItemId, string Evidence, long Attempts)> items,
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
        int limit, CancellationToken ct)
    {
        // Ordem e corte no banco: Attempts é número, que os dois bancos ordenam.
        // Antes vinha a tabela inteira e a ordem era feita aqui.
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CloudSuspectItems.AsNoTracking()
            .Where(s => s.VaultId == vaultId && (status == null || s.Status == status))
            .OrderByDescending(s => s.Attempts)
            .ThenByDescending(s => s.Id)
            .Take(limit)
            .ToListAsync(ct);
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

    // As três listas de decisão (em dúvida, incidentes, quarentena) paginam
    // como a auditoria logo abaixo: cursor no Id, uma linha a mais para saber
    // se há próxima, ordem no banco. Antes cada uma trazia a tabela inteira e
    // ordenava aqui — e o que já foi decidido nunca expira.

    public async Task<CursorPage<CloudDoubtfulOperation>> ListDoubtfulAsync(Guid vaultId, bool openOnly,
        Guid? before, int pageSize, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = db.CloudDoubtfulOperations.AsNoTracking()
            .Where(d => d.VaultId == vaultId && (!openOnly || d.Status == CloudDoubtfulStatus.Open));
        if (before is { } cursor)
            query = query.Where(d => d.Id.CompareTo(cursor) < 0);

        var rows = await query.OrderByDescending(d => d.Id).Take(pageSize + 1).ToListAsync(ct);
        return CursorPage.From(rows, pageSize, d => d.Id);
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

    public async Task<CursorPage<CloudRollbackIncident>> ListIncidentsAsync(Guid vaultId, bool openOnly,
        Guid? before, int pageSize, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = db.CloudRollbackIncidents.AsNoTracking()
            .Where(i => i.VaultId == vaultId && (!openOnly || i.Status == CloudIncidentStatus.Open));
        if (before is { } cursor)
            query = query.Where(i => i.Id.CompareTo(cursor) < 0);

        var rows = await query.OrderByDescending(i => i.Id).Take(pageSize + 1).ToListAsync(ct);
        return CursorPage.From(rows, pageSize, i => i.Id);
    }

    public async Task<CloudRollbackIncident?> GetIncidentAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CloudRollbackIncidents.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task<IReadOnlyDictionary<string, (long Epoch, long Seq)>> WorldWatermarksAsync(Guid vaultId,
        Guid serverId, CancellationToken ct)
    {
        // Uma linha por jogador, pelo índice (VaultId, ServerId, PlayerUuid).
        // Antes eram TODOS os lotes aplicados do servidor, reduzidos em memória.
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CloudWatermarks.AsNoTracking()
            .Where(w => w.VaultId == vaultId && w.ServerId == serverId)
            .ToDictionaryAsync(w => w.PlayerUuid, w => (w.Epoch, w.Seq), ct);
    }

    // ---------------------------------------------------------------- quarentena

    public async Task<CursorPage<CloudQuarantineView>> ListQuarantineAsync(Guid vaultId, bool openOnly,
        Guid? before, int pageSize, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var query =
            from q in db.CloudQuarantine.AsNoTracking()
            join b in db.CloudBatches.AsNoTracking() on q.BatchId equals b.Id
            where q.VaultId == vaultId && (!openOnly || q.ResolvedAt == null)
            select new { q, b };
        if (before is { } cursor)
            query = query.Where(r => r.q.Id.CompareTo(cursor) < 0);

        var rows = await query.OrderByDescending(r => r.q.Id).Take(pageSize + 1).ToListAsync(ct);
        return CursorPage.From([.. rows.Select(r => new CloudQuarantineView(r.q, r.b))], pageSize,
            v => v.Quarantine.Id);
    }

    public async Task<CloudOpenCounts> CountOpenAsync(Guid vaultId, CancellationToken ct)
    {
        // Quatro COUNT pelos índices (VaultId, Status/ResolvedAt), no mesmo
        // contexto, um depois do outro (um DbContext não roda consultas em paralelo).
        await using var db = await factory.CreateDbContextAsync(ct);
        var suspects = await db.CloudSuspectItems
            .CountAsync(s => s.VaultId == vaultId && s.Status == CloudSuspectStatus.Pending, ct);
        var quarantine = await db.CloudQuarantine
            .CountAsync(q => q.VaultId == vaultId && q.ResolvedAt == null, ct);
        var doubtful = await db.CloudDoubtfulOperations
            .CountAsync(d => d.VaultId == vaultId && d.Status == CloudDoubtfulStatus.Open, ct);
        var incidents = await db.CloudRollbackIncidents
            .CountAsync(i => i.VaultId == vaultId && i.Status == CloudIncidentStatus.Open, ct);
        return new CloudOpenCounts(suspects, quarantine, doubtful, incidents);
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

    // As duas listas da auditoria paginam por cursor no Id (GUID v7, então
    // "Id menor" = "mais velho"): a página seguinte é "as N anteriores ao
    // último Id visto", que o índice (VaultId, Id) responde sem varrer o que
    // ficou para trás. Busca-se UMA linha a mais só para saber se há próxima.
    // CompareTo, e não "<": é a forma que o EF traduz para comparação no SQL.

    public async Task<CursorPage<CloudAdminAuditEntry>> ListAuditAsync(Guid vaultId, Guid? before, int pageSize,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = db.CloudAdminAudit.AsNoTracking().Where(a => a.VaultId == vaultId);
        if (before is { } cursor)
            query = query.Where(a => a.Id.CompareTo(cursor) < 0);

        var rows = await query.OrderByDescending(a => a.Id).Take(pageSize + 1).ToListAsync(ct);
        return CursorPage.From(rows, pageSize, a => a.Id);
    }

    public async Task<CursorPage<CloudLedgerView>> ListLedgerAsync(CloudLedgerQuery query, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // Filtra pela coluna da nuvem na própria linha (índice VaultId, Id); os
        // joins com canal e item ficam só para as linhas da página.
        var entries = db.CloudLedger.AsNoTracking().Where(e => e.VaultId == query.VaultId);

        if (query.PlayerUuid is { } player)
        {
            var channels = db.CloudChannels
                .Where(c => c.VaultId == query.VaultId && c.PlayerUuid == player)
                .Select(c => c.Id);
            entries = entries.Where(e => channels.Contains(e.ChannelId));
        }

        if (query.Source is { } source)
            entries = entries.Where(e => e.Source == source);

        // Período pelo Id, não pelo CreatedAt: o SQLite não compara
        // DateTimeOffset, e o Id já carrega o instante (ver IdTime).
        if (query.From is { } periodStart)
        {
            var lower = IdTime.Floor(periodStart);
            entries = entries.Where(e => e.Id.CompareTo(lower) >= 0);
        }

        if (query.Until is { } periodEnd)
        {
            var upper = IdTime.Floor(periodEnd);
            entries = entries.Where(e => e.Id.CompareTo(upper) < 0);
        }

        if (query.Before is { } cursor)
            entries = entries.Where(e => e.Id.CompareTo(cursor) < 0);

        if (query.Item is { } item)
        {
            // lower() dos dois lados: o LIKE do PostgreSQL diferencia
            // maiúsculas, e "diamond" tem de achar "Diamond".
            var term = item.ToLowerInvariant();
            // ToLower() sem cultura de propósito: vira lower() no SQL. As
            // sobrecargas com StringComparison ou cultura que os analisadores
            // pedem não têm tradução e quebrariam a consulta.
#pragma warning disable CA1304, CA1311, CA1862
            var items = db.CloudItemTypes
                .Where(i => i.ItemId.ToLower().Contains(term) || i.DisplayName.ToLower().Contains(term))
                .Select(i => i.Id);
#pragma warning restore CA1304, CA1311, CA1862
            entries = entries.Where(e => items.Contains(e.ItemTypeId));
        }

        var rows = await (
            from e in entries
            join c in db.CloudChannels.AsNoTracking() on e.ChannelId equals c.Id
            join i in db.CloudItemTypes.AsNoTracking() on e.ItemTypeId equals i.Id
            orderby e.Id descending
            select new
            {
                e.Id,
                e.CreatedAt,
                c.PlayerUuid,
                ChannelName = c.Name,
                i.ItemId,
                i.DisplayName,
                e.Delta,
                e.BalanceAfter,
                e.Source,
                e.Reason
            }
        ).Take(query.PageSize + 1).ToListAsync(ct);

        // Nome da conta do TCMine de cada jogador da página (MinecraftUuid é
        // único e tem o mesmo formato do canal: 32 hex, sem hífens).
        var uuids = rows.Select(r => r.PlayerUuid).Distinct().ToArray();
        var names = await db.Users.AsNoTracking()
            .Where(u => u.MinecraftUuid != null && uuids.Contains(u.MinecraftUuid))
            .ToDictionaryAsync(u => u.MinecraftUuid!, u => u.DisplayName, ct);
        var views = rows.Select(r => new CloudLedgerView(r.Id, r.CreatedAt, r.PlayerUuid,
            names.GetValueOrDefault(r.PlayerUuid), r.ChannelName, r.ItemId, r.DisplayName, r.Delta, r.BalanceAfter,
            r.Source, r.Reason)).ToList();
        return CursorPage.From(views, query.PageSize, v => v.Id);
    }
}
