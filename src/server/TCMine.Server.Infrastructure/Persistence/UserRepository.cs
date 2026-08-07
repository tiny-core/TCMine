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

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // Normaliza dos dois lados: o e-mail é gravado em minúsculas, mas quem
        // digita no login não tem obrigação de saber disso.
        var normalized = email.ToLowerInvariant();
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == normalized, ct);
    }

    public async Task<User?> GetByMicrosoftObjectIdAsync(string objectId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.MicrosoftObjectId == objectId, ct);
    }

    public async Task AddAsync(User user, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(User user, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Users.Update(user);
        await db.SaveChangesAsync(ct);
    }
}
