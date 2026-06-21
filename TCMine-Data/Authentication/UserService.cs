using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TCMine_Data.Data;
using TCMine_Data.Entities;

namespace TCMine_Data.Authentication;

/// <summary>
/// Operações de usuários do painel: criação, validação de credenciais e consulta.
/// Hash de senha com <see cref="PasswordHasher{TUser}"/> (PBKDF2). Scoped — usa o
/// <see cref="AppDbContext"/> da requisição/circuito.
/// </summary>
public sealed class UserService(AppDbContext db)
{
    private readonly AppDbContext _db = db;

    // PasswordHasher é stateless e thread-safe; instanciar diretamente é suficiente
    private readonly PasswordHasher<UserEntity> _hasher = new();


    /// <summary>Existe ao menos um usuário? Usado para detectar a primeira execução.</summary>
    public Task<bool> AnyUsersExistAsync(CancellationToken ct = default)
    {
        return _db.Users.AnyAsync(ct);
    }

    /// <summary>Cria um usuário com a senha já transformada em hash.</summary>
    public async Task<UserEntity> CreateAsync(
        string username, string password, UserRole role, CancellationToken ct = default)
    {
        var user = new UserEntity { Username = Normalize(username), Role = role };
        user.PasswordHash = _hasher.HashPassword(user, password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return user;
    }

    /// <summary>
    /// Valida usuário+senha. Devolve o usuário em caso de sucesso (e atualiza LastLoginAt),
    /// ou null se as credenciais forem inválidas ou a conta estiver inativa.
    /// </summary>
    public async Task<UserEntity?> ValidateCredentialsAsync(
        string username, string password, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == Normalize(username), ct);
        if (user is null || !user.IsActive) return null;

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed) return null;

        // O algoritmo de hash evoluiu desde que a senha foi gravada — regrava com o novo
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = _hasher.HashPassword(user, password);

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return user;
    }

    /// <summary>Todos os usuários, ordenados por nome (para a tela de gestão).</summary>
    public Task<List<UserEntity>> GetAllAsync(CancellationToken ct = default)
    {
        return _db.Users.OrderBy(u => u.Username).ToListAsync(ct);
    }

    /// <summary>True se já existe um usuário com esse nome (login é único).</summary>
    public Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default)
    {
        return _db.Users.AnyAsync(u => u.Username == Normalize(username), ct);
    }

    /// <summary>
    /// Quantos Owners ativos existem. Usado para impedir remover/rebaixar/desativar o último —
    /// sem isso, dava para ficar trancado fora da gestão de usuários e dos secrets.
    /// </summary>
    public Task<int> CountActiveOwnersAsync(CancellationToken ct = default)
    {
        return _db.Users.CountAsync(u => u.Role == UserRole.Owner && u.IsActive, ct);
    }

    /// <summary>Ativa/desativa um usuário (conta inativa não loga).</summary>
    public async Task SetActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync([id], ct);
        if (user is null) return;
        user.IsActive = active;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Troca o papel de um usuário.</summary>
    public async Task ChangeRoleAsync(Guid id, UserRole role, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync([id], ct);
        if (user is null) return;
        user.Role = role;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Remove um usuário definitivamente.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync([id], ct);
        if (user is null) return;
        _db.Users.Remove(user);
        await _db.SaveChangesAsync(ct);
    }

    // Login case-insensitive: normaliza para minúsculas antes de gravar/consultar
    private static string Normalize(string username)
    {
        return username.Trim().ToLowerInvariant();
    }
}