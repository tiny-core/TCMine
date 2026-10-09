using Microsoft.EntityFrameworkCore;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Infrastructure.Persistence;

public sealed class UserRepository(IDbContextFactory<TcMineDbContext> factory) : IUserRepository
{
    public async Task<bool> AnyAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Users.AnyAsync(ct);
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<User?> GetByMicrosoftObjectIdAsync(string objectId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.MicrosoftObjectId == objectId, ct);
    }

    public async Task<User?> GetByMinecraftUuidAsync(string uuid, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // A Mojang devolve o UUID em minúsculas e sem hífens; normalizar aqui
        // evita depender de o chamador ter feito isso.
        var normalized = uuid.Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.MinecraftUuid == normalized, ct);
    }

    public async Task AddAsync(User user, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> TryAddAsync(User user, CancellationToken ct)
    {
        try
        {
            await AddAsync(user, ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Só é "perdi a corrida" se a identidade de fato já está lá; qualquer
            // outra falha de gravação continua sendo erro e sobe. Contexto novo:
            // o que falhou ficou com a entidade presa no estado Added.
            await using var db = await factory.CreateDbContextAsync(ct);
            var taken = await db.Users.AnyAsync(u =>
                (user.MinecraftUuid != null && u.MinecraftUuid == user.MinecraftUuid)
                || (user.MicrosoftObjectId != null && u.MicrosoftObjectId == user.MicrosoftObjectId), ct);

            if (taken)
                return false;

            throw;
        }
    }

    public async Task MergeAsync(Guid keepId, Guid absorbedId, CancellationToken ct)
    {
        if (keepId == absorbedId)
            return;

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Vínculos têm índice único (usuário, recurso): re-apontar às cegas
        // estouraria quando as duas contas já estão no mesmo servidor. Nesse
        // caso fica o papel maior e o vínculo da absorvida morre.
        var keptServers = await db.Memberships.Where(m => m.UserId == keepId).ToListAsync(ct);
        foreach (var m in await db.Memberships.Where(m => m.UserId == absorbedId).ToListAsync(ct))
        {
            if (keptServers.FirstOrDefault(k => k.GameServerId == m.GameServerId) is { } kept)
            {
                if (m.Role > kept.Role)
                    kept.Role = m.Role;
                db.Memberships.Remove(m);
            }
            else
                m.UserId = keepId;
        }

        var keptModpacks = await db.ModpackMemberships.Where(m => m.UserId == keepId).ToListAsync(ct);
        foreach (var m in await db.ModpackMemberships.Where(m => m.UserId == absorbedId).ToListAsync(ct))
        {
            if (keptModpacks.FirstOrDefault(k => k.ModpackId == m.ModpackId) is { } kept)
            {
                if (m.Role > kept.Role)
                    kept.Role = m.Role;
                db.ModpackMemberships.Remove(m);
            }
            else
                m.UserId = keepId;
        }

        await db.SaveChangesAsync(ct);

        // O resto é ponteiro simples, sem índice único: UPDATE direto.
        await db.AccessRequests.Where(r => r.UserId == absorbedId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.UserId, keepId), ct);
        await db.AccessRequests.Where(r => r.ResolvedByUserId == absorbedId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.ResolvedByUserId, keepId), ct);
        await db.Invites.Where(i => i.RedeemedByUserId == absorbedId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.RedeemedByUserId, keepId), ct);
        await db.Invites.Where(i => i.CreatedByUserId == absorbedId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.CreatedByUserId, keepId), ct);
        await db.GameServers.Where(g => g.OwnerId == absorbedId)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.OwnerId, keepId), ct);
        await db.Modpacks.Where(m => m.OwnerId == absorbedId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.OwnerId, keepId), ct);
        await db.CloudVaults.Where(v => v.OwnerId == absorbedId)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.OwnerId, keepId), ct);

        await db.Users.Where(u => u.Id == absorbedId).ExecuteDeleteAsync(ct);

        await tx.CommitAsync(ct);
    }

    public async Task UpdateAsync(User user, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Users.Update(user);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Users
            .AsNoTracking()
            .OrderBy(u => u.DisplayName)
            .ToListAsync(ct);
    }
}
