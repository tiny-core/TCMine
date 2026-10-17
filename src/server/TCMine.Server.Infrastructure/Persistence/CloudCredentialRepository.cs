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

    public async Task<CloudServerCredential?> FindByServerAsync(Guid gameServerId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CloudServerCredentials.AsNoTracking()
            .FirstOrDefaultAsync(c => c.GameServerId == gameServerId, ct);
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

    public async Task RevokeAsync(Guid gameServerId, DateTimeOffset now, CancellationToken ct)
    {
        // A conversão explícita: as colunas são anuláveis, e sem ela a
        // inferência do SetProperty fica entre DateTimeOffset e DateTimeOffset?.
        DateTimeOffset? at = now;

        await using var db = await factory.CreateDbContextAsync(ct);
        await db.CloudServerCredentials
            .Where(c => c.GameServerId == gameServerId && c.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.RevokedAt, at)
                .SetProperty(c => c.UpdatedAt, at), ct);
    }

    public async Task RecordContactAsync(Guid credentialId, DateTimeOffset now, string? modVersion, Guid? worldId,
        CancellationToken ct)
    {
        var version = CloudServerCredential.TrimModVersion(modVersion);
        DateTimeOffset? at = now;

        await using var db = await factory.CreateDbContextAsync(ct);
        await db.CloudServerCredentials
            .Where(c => c.Id == credentialId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.LastSeenAt, at)
                .SetProperty(c => c.ModVersion, c => version ?? c.ModVersion)
                .SetProperty(c => c.WorldId, c => worldId ?? c.WorldId)
                .SetProperty(c => c.UpdatedAt, at), ct);
    }

    public async Task<CloudVault?> GetVaultAsync(Guid vaultId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.CloudVaults.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vaultId, ct);
    }
}
