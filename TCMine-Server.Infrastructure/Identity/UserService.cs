using Microsoft.AspNetCore.Identity;
using TCMine_Application.Abstractions;
using TCMine_Domain.Identity;
using TCMine_Domain.Entities;

namespace TCMine_Server.Infrastructure.Identity;

/// <summary>
/// Operações de usuários do painel: criação, validação de credenciais e consulta.
/// Hash de senha com <see cref="PasswordHasher{TUser}"/> (PBKDF2). A persistência fica atrás de
/// <see cref="IUserRepository"/> — este serviço orquestra as regras (normalização do login, hash,
/// proteção do último Owner) e delega o acesso ao banco. Scoped.
/// </summary>
public sealed class UserService(IUserRepository users)
{
    // PasswordHasher é stateless e thread-safe; instanciar diretamente é suficiente
    private readonly PasswordHasher<UserEntity> _hasher = new();

    /// <summary>Existe ao menos um usuário? Usado para detectar a primeira execução.</summary>
    public Task<bool> AnyUsersExistAsync(CancellationToken ct = default)
    {
        return users.AnyAsync(ct);
    }

    /// <summary>
    /// Cria um usuário com a senha já transformada em hash. Lança
    /// <see cref="InvalidOperationException"/> se o login já existir (é único).
    /// </summary>
    public async Task<UserEntity> CreateAsync(
        string username, string password, UserRole role, CancellationToken ct = default)
    {
        var normalized = Normalize(username);
        if (await users.ExistsByUsernameAsync(normalized, ct))
            throw new InvalidOperationException("Já existe um usuário com esse login.");

        var user = new UserEntity { Username = normalized, Role = role };
        user.PasswordHash = _hasher.HashPassword(user, password);

        users.Add(user);
        await users.SaveChangesAsync(ct);
        return user;
    }

    /// <summary>
    /// Valida usuário+senha. Devolve o usuário em caso de sucesso (e atualiza LastLoginAt),
    /// ou null se as credenciais forem inválidas ou a conta estiver inativa.
    /// </summary>
    public async Task<UserEntity?> ValidateCredentialsAsync(
        string username, string password, CancellationToken ct = default)
    {
        var user = await users.GetByUsernameAsync(Normalize(username), ct);
        if (user is null || !user.IsActive) return null;

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed) return null;

        // O algoritmo de hash evoluiu desde que a senha foi gravada — regrava com o novo
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = _hasher.HashPassword(user, password);

        user.LastLoginAt = DateTime.UtcNow;
        await users.SaveChangesAsync(ct);
        return user;
    }

    /// <summary>Todos os usuários, ordenados por nome (para a tela de gestão).</summary>
    public Task<List<UserEntity>> GetAllAsync(CancellationToken ct = default)
    {
        return users.GetAllOrderedAsync(ct);
    }

    /// <summary>True se já existe um usuário com esse nome (login é único).</summary>
    public Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default)
    {
        return users.ExistsByUsernameAsync(Normalize(username), ct);
    }

    /// <summary>
    /// Quantos Owners ativos existem. Usado para impedir remover/rebaixar/desativar o último —
    /// sem isso, dava para ficar trancado fora da gestão de usuários e dos secrets.
    /// </summary>
    public Task<int> CountActiveOwnersAsync(CancellationToken ct = default)
    {
        return users.CountActiveOwnersAsync(ct);
    }

    /// <summary>
    /// Atualiza um usuário existente: login, papel, estado ativo e, opcionalmente, a senha
    /// (só regrava o hash quando <paramref name="newPassword"/> vem preenchido). Numa única
    /// transação. Lança <see cref="InvalidOperationException"/> se o login novo colidir com
    /// outro usuário ou se a mudança deixar o sistema sem nenhum Owner ativo.
    /// </summary>
    public async Task UpdateAsync(
        Guid id, string username, UserRole role, bool isActive, string? newPassword,
        CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(id, ct);
        if (user is null) return;

        var normalized = Normalize(username);
        // Login mudou? Garante que o novo nome ainda é único
        if (normalized != user.Username && await users.ExistsByUsernameAsync(normalized, ct))
            throw new InvalidOperationException("Já existe um usuário com esse login.");

        // Rebaixar ou desativar o último Owner ativo trancaria o sistema fora da gestão
        if (WouldLoseLastOwner(user, role, isActive) && await users.CountActiveOwnersAsync(ct) <= 1)
            throw new InvalidOperationException("Não é possível rebaixar ou desativar o último Owner ativo.");

        user.Username = normalized;
        user.Role = role;
        user.IsActive = isActive;
        if (!string.IsNullOrEmpty(newPassword))
            user.PasswordHash = _hasher.HashPassword(user, newPassword);

        await users.SaveChangesAsync(ct);
    }

    /// <summary>Ativa/desativa um usuário (conta inativa não loga). Protege o último Owner ativo.</summary>
    public async Task SetActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(id, ct);
        if (user is null) return;

        if (!active && WouldLoseLastOwner(user, user.Role, false) && await users.CountActiveOwnersAsync(ct) <= 1)
            throw new InvalidOperationException("Não é possível desativar o último Owner ativo.");

        user.IsActive = active;
        await users.SaveChangesAsync(ct);
    }

    /// <summary>Remove um usuário definitivamente. Protege o último Owner ativo.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(id, ct);
        if (user is null) return;

        if (user is { Role: UserRole.Owner, IsActive: true } && await users.CountActiveOwnersAsync(ct) <= 1)
            throw new InvalidOperationException("Não é possível remover o último Owner ativo.");

        users.Remove(user);
        await users.SaveChangesAsync(ct);
    }

    // True se o usuário era Owner ativo e a mudança o tira desse estado (rebaixa ou desativa)
    private static bool WouldLoseLastOwner(UserEntity user, UserRole newRole, bool newActive)
    {
        var wasActiveOwner = user is { Role: UserRole.Owner, IsActive: true };
        var willBeActiveOwner = newRole == UserRole.Owner && newActive;
        return wasActiveOwner && !willBeActiveOwner;
    }

    // Login case-insensitive: normaliza para minúsculas antes de gravar/consultar
    private static string Normalize(string username)
    {
        return username.Trim().ToLowerInvariant();
    }
}