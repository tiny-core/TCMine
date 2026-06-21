using Microsoft.EntityFrameworkCore;
using TCMine_Application.Abstractions;
using TCMine_Domain.Entities;
using TCMine_Domain.Identity;

namespace TCMine_Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementação EF Core de <see cref="IUserRepository"/>. Scoped — usa o <see cref="AppDbContext"/>
/// da requisição/circuito. Só persistência; a lógica de hash/normalização fica no serviço.
/// </summary>
public sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<bool> AnyAsync(CancellationToken ct = default)
    {
        return db.Users.AnyAsync(ct);
    }

    public Task<UserEntity?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        return db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);
    }

    public async Task<UserEntity?> FindByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await db.Users.FindAsync([id], ct);
    }

    public Task<List<UserEntity>> GetAllOrderedAsync(CancellationToken ct = default)
    {
        return db.Users.OrderBy(u => u.Username).ToListAsync(ct);
    }

    public Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default)
    {
        return db.Users.AnyAsync(u => u.Username == username, ct);
    }

    public Task<int> CountActiveOwnersAsync(CancellationToken ct = default)
    {
        return db.Users.CountAsync(u => u.Role == UserRole.Owner && u.IsActive, ct);
    }

    public void Add(UserEntity user)
    {
        db.Users.Add(user);
    }

    public void Remove(UserEntity user)
    {
        db.Users.Remove(user);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return db.SaveChangesAsync(ct);
    }
}