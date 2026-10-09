using Microsoft.EntityFrameworkCore;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Infrastructure.Persistence;

public sealed class CloudCredentialRepository(IDbContextFactory<TcMineDbContext> factory) : ICloudCredentialRepository
{
    public async Task<CloudServerCredential?> FindByPrefixAsync(string prefix, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CloudServerCredentials.AsNoTracking().FirstOrDefaultAsync(c => c.KeyPrefix == prefix, ct);
    }

    public async Task<CloudServerCredential?> FindByIdAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CloudServerCredentials.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<IReadOnlyList<CloudServerCredential>> ListActiveByServerAsync(Guid gameServerId,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CloudServerCredentials.AsNoTracking()
            .Where(c => c.GameServerId == gameServerId && c.RevokedAt == null)
            .ToListAsync(ct);
    }

    public async Task AddAsync(CloudServerCredential credential, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.CloudServerCredentials.Add(credential);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(CloudServerCredential credential, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.CloudServerCredentials.Update(credential);
        await db.SaveChangesAsync(ct);
    }

    public async Task<CloudVault?> GetVaultAsync(Guid vaultId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CloudVaults.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vaultId, ct);
    }
}
