using Microsoft.EntityFrameworkCore;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Infrastructure.Persistence;

public sealed class CloudCredentialRepository(IDbContextFactory<TcMineDbContext> factory) : ICloudCredentialRepository
{
    public async Task<CloudKeyLookup?> FindKeyAsync(string prefix, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        // A nuvem do servidor vem de uma subconsulta, que dá nulo quando o
        // servidor não existe mais: a chave dele ainda é achada, e a recusa diz
        // "não está ligado" em vez de "chave inválida". Continua uma ida só ao banco.
        return await db.CloudServerCredentials.AsNoTracking()
            .Where(c => c.KeyPrefix == prefix)
            .Select(c => new CloudKeyLookup(c.Id, c.GameServerId, c.VaultId, c.KeyHash, c.RevokedAt == null,
                db.GameServers.Where(s => s.Id == c.GameServerId).Select(s => s.CloudVaultId).FirstOrDefault()))
            .FirstOrDefaultAsync(ct);
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
