using Microsoft.EntityFrameworkCore;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Infrastructure.Persistence;

public sealed class CloudAdminRepository(IDbContextFactory<TcMineDbContext> factory) : ICloudAdminRepository
{
    public async Task AddVaultAsync(CloudVault vault, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.CloudVaults.Add(vault);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateVaultAsync(CloudVault vault, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.CloudVaults.Update(vault);
        await db.SaveChangesAsync(ct);
    }

    public async Task<CloudVault?> GetVaultAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CloudVaults.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, ct);
    }

    public async Task<IReadOnlyList<CloudVaultSummary>> ListVaultsAsync(Guid? ownerId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var vaults = await db.CloudVaults.AsNoTracking()
            .Where(v => ownerId == null || v.OwnerId == ownerId)
            .ToListAsync(ct);
        var ids = vaults.Select(v => v.Id).ToArray();

        var servers = await db.GameServers.AsNoTracking()
            .Where(s => s.CloudVaultId != null && ids.Contains(s.CloudVaultId.Value))
            .GroupBy(s => s.CloudVaultId!.Value)
            .Select(g => new { Vault = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Vault, x => x.Count, ct);
        var players = await db.CloudChannels.AsNoTracking()
            .Where(c => ids.Contains(c.VaultId))
            .Select(c => new { c.VaultId, c.PlayerUuid })
            .Distinct()
            .GroupBy(c => c.VaultId)
            .Select(g => new { Vault = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Vault, x => x.Count, ct);

        return
        [
            .. vaults.OrderBy(v => v.Id).Select(v => new CloudVaultSummary(v.Id, v.Name, v.OwnerId, v.IsEnabled,
                servers.GetValueOrDefault(v.Id), players.GetValueOrDefault(v.Id)))
        ];
    }

    public async Task<IReadOnlyList<CloudServerKeyView>> ListActiveKeysAsync(IReadOnlyCollection<Guid> serverIds,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var keys = await db.CloudServerCredentials.AsNoTracking()
            .Where(c => serverIds.Contains(c.GameServerId) && c.RevokedAt == null)
            .ToListAsync(ct);
        return
        [
            .. keys.Select(k =>
                new CloudServerKeyView(k.GameServerId, k.KeyPrefix, k.IssuedAt, k.LastSeenAt, k.ModVersion))
        ];
    }

    public async Task<PagedResult<CloudPlayerView>> ListPlayersAsync(Guid vaultId, string? search, PageRequest page,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // Um jogador = um UUID com pelo menos um canal nesta nuvem. O nome vem
        // da conta do TCMine numa subconsulta (MinecraftUuid é único, então
        // cada UUID dá no máximo um nome). Filtro, ordem e página no banco:
        // antes, todos os canais da nuvem vinham para a memória a cada busca.
        var players = db.CloudChannels.AsNoTracking()
            .Where(c => c.VaultId == vaultId)
            .Select(c => c.PlayerUuid)
            .Distinct()
            .Select(uuid => new
            {
                Uuid = uuid,
                Name = db.Users.Where(u => u.MinecraftUuid == uuid).Select(u => u.DisplayName).FirstOrDefault()
            });

        if (search is not null)
        {
            // UUID sem hífens e em minúsculas, como o canal guarda; nome com
            // lower() dos dois lados, porque o LIKE do PostgreSQL diferencia
            // maiúsculas e "ana" tem de achar "Ana".
            // ToLower() sem cultura de propósito: vira lower() no SQL (as
            // sobrecargas com StringComparison/cultura não têm tradução).
            var uuidPart = search.Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();
            var namePart = search.ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862
            players = players.Where(p => p.Uuid.Contains(uuidPart)
                                         || (p.Name != null && p.Name.ToLower().Contains(namePart)));
#pragma warning restore CA1304, CA1311, CA1862
        }

        var total = await players.CountAsync(ct);

        // Quem tem conta primeiro, por nome; os sem conta depois, por UUID. O
        // UUID desempata nomes iguais — sem desempate, a mesma linha podia
        // aparecer em duas páginas.
#pragma warning disable CA1304, CA1311
        var pageRows = await players
            .OrderBy(p => p.Name == null)
            .ThenBy(p => p.Name!.ToLower())
            .ThenBy(p => p.Uuid)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(ct);
#pragma warning restore CA1304, CA1311
        if (pageRows.Count == 0)
            return new PagedResult<CloudPlayerView>([], total);

        var uuids = pageRows.Select(p => p.Uuid).ToArray();
        var channels = await db.CloudChannels.AsNoTracking()
            .Where(c => c.VaultId == vaultId && uuids.Contains(c.PlayerUuid))
            .ToListAsync(ct);
        var channelIds = channels.Select(c => c.Id).ToArray();
        var totals = await db.CloudBalances.AsNoTracking()
            .Where(b => channelIds.Contains(b.ChannelId) && b.Amount > 0)
            .GroupBy(b => b.ChannelId)
            .Select(g => new { Channel = g.Key, Types = g.Count(), Total = g.Sum(b => b.Amount) })
            .ToDictionaryAsync(x => x.Channel, ct);
        var leases = await db.CloudLeases.AsNoTracking()
            .Where(l => l.VaultId == vaultId && uuids.Contains(l.PlayerUuid))
            .ToDictionaryAsync(l => l.PlayerUuid, ct);

        return new PagedResult<CloudPlayerView>(
        [
            .. pageRows.Select(p =>
            {
                var lease = leases.GetValueOrDefault(p.Uuid);
                var views = channels.Where(c => c.PlayerUuid == p.Uuid).OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
                    .Select(c =>
                    {
                        var t = totals.GetValueOrDefault(c.Id);
                        return new CloudChannelView(c.Id, c.Name, c.IsFrozen, c.FrozenReason, t?.Types ?? 0,
                            t?.Total ?? 0);
                    }).ToArray();
                return new CloudPlayerView(p.Uuid, p.Name, views, lease?.HolderServerId, lease?.Epoch ?? 0,
                    lease?.ExpiresAt);
            })
        ], total);
    }

    public async Task<IReadOnlyList<CloudBalanceView>> ListChannelBalancesAsync(Guid channelId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await (
            from b in db.CloudBalances.AsNoTracking()
            join i in db.CloudItemTypes.AsNoTracking() on b.ItemTypeId equals i.Id
            where b.ChannelId == channelId && b.Amount > 0
            select new
            {
                i.Fingerprint,
                i.ItemId,
                i.ModId,
                i.DisplayName,
                b.Amount
            }
        ).ToListAsync(ct);
        return
        [
            .. rows.OrderByDescending(r => r.Amount)
                .Select(r => new CloudBalanceView(r.Fingerprint, r.ItemId, r.ModId, r.DisplayName, r.Amount))
        ];
    }

    public async Task<CloudChannel?> GetChannelAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CloudChannels.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task UpdateChannelAsync(CloudChannel channel, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.CloudChannels.Update(channel);
        await db.SaveChangesAsync(ct);
    }
}
