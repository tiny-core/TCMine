using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Security;

/// <summary>
///     Cria o administrador da instalação no primeiro acesso.
///     Só funciona enquanto NÃO existe nenhum usuário — depois disso a rota de
///     setup fecha sozinha, senão qualquer visitante viraria admin.
/// </summary>
public sealed class CreateFirstAdmin(IUserRepository users, IPasswordHasher hasher)
{
    /// <summary>Tamanho mínimo de senha. Curto o bastante para não irritar, longo o bastante para não ser trivial.</summary>
    public const int MinPasswordLength = 8;

    public async Task<Result<Guid>> HandleAsync(
        string email, string displayName, string password, CancellationToken ct)
    {
        if (await users.AnyAsync(ct))
            return Result<Guid>.Fail("A instalação já tem um administrador.");

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return Result<Guid>.Fail("Informe um e-mail válido.");

        if (string.IsNullOrWhiteSpace(displayName))
            return Result<Guid>.Fail("Informe um nome de exibição.");

        if (password.Length < MinPasswordLength)
            return Result<Guid>.Fail($"A senha precisa de pelo menos {MinPasswordLength} caracteres.");

        var user = new User
        {
            Email = email.Trim().ToLowerInvariant(),
            DisplayName = displayName.Trim(),
            PasswordHash = hasher.Hash(password),
            IsInstanceAdmin = true
        };

        await users.AddAsync(user, ct);
        return Result<Guid>.Success(user.Id);
    }
}
