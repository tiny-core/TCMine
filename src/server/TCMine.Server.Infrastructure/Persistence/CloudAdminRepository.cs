using Microsoft.EntityFrameworkCore;
using TCMine.Server.Application.Abstractions;
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
                new CloudServerKeyView(k.GameServerId, k.KeyPrefix, k.CreatedAt, k.LastSeenAt, k.ModVersion))
        ];
    }

    public async Task<IReadOnlyList<CloudPlayerView>> ListPlayersAsync(Guid vaultId, string? search, int limit,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var channels = await db.CloudChannels.AsNoTracking().Where(c => c.VaultId == vaultId).ToListAsync(ct);
        var uuids = channels.Select(c => c.PlayerUuid).Distinct().ToArray();

        // Nome pela conta do TCMine quando o jogador tem uma (o UUID do
        // Minecraft fica em User.MinecraftUuid, sem hífens).
        var names = (await db.Users.AsNoTracking()
                .Where(u => u.MinecraftUuid != null && uuids.Contains(u.MinecraftUuid))
                .Select(u => new { u.MinecraftUuid, u.DisplayName })
                .ToListAsync(ct))
            .GroupBy(u => u.MinecraftUuid!)
            .ToDictionary(g => g.Key, g => g.First().DisplayName);

        var selected = uuids
            .Where(uuid => search is null
                           || uuid.Contains(search.Replace("-", ""), StringComparison.OrdinalIgnoreCase)
                           || (names.TryGetValue(uuid, out var n) &&
                               n.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(uuid => names.GetValueOrDefault(uuid) ?? "~" + uuid, StringComparer.CurrentCultureIgnoreCase)
            .Take(limit)
            .ToHashSet();

        var channelIds = channels.Where(c => selected.Contains(c.PlayerUuid)).Select(c => c.Id).ToArray();
        var totals = await db.CloudBalances.AsNoTracking()
            .Where(b => channelIds.Contains(b.ChannelId) && b.Amount > 0)
            .GroupBy(b => b.ChannelId)
            .Select(g => new { Channel = g.Key, Types = g.Count(), Total = g.Sum(b => b.Amount) })
            .ToDictionaryAsync(x => x.Channel, ct);
        var leases = await db.CloudLeases.AsNoTracking()
            .Where(l => l.VaultId == vaultId && selected.Contains(l.PlayerUuid))
            .ToDictionaryAsync(l => l.PlayerUuid, ct);

        return
        [
            .. selected.Select(uuid =>
            {
                var lease = leases.GetValueOrDefault(uuid);
                var views = channels.Where(c => c.PlayerUuid == uuid).OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
                    .Select(c =>
                    {
                        var t = totals.GetValueOrDefault(c.Id);
                        return new CloudChannelView(c.Id, c.Name, c.IsFrozen, c.FrozenReason, t?.Types ?? 0,
                            t?.Total ?? 0);
                    }).ToArray();
                return new CloudPlayerView(uuid, names.GetValueOrDefault(uuid), views, lease?.HolderServerId,
                    lease?.Epoch ?? 0, lease?.ExpiresAt);
            }).OrderBy(p => p.DisplayName ?? "~" + p.PlayerUuid, StringComparer.CurrentCultureIgnoreCase)
        ];
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
