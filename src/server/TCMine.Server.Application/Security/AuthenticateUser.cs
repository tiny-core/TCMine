using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Security;

/// <summary>
///     Login com conta local (e-mail + senha).
/// </summary>
public sealed class AuthenticateUser(IUserRepository users, IPasswordHasher hasher)
{
    public async Task<Result<User>> HandleAsync(string email, string password, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return Result<User>.Fail("Informe e-mail e senha.");

        var user = await users.GetByEmailAsync(email.Trim(), ct);

        // Mensagem única para "não existe" e "senha errada": dizer qual dos dois
        // falhou entregaria a enumeração de contas a quem está tentando adivinhar.
        if (user?.PasswordHash is null)
            return Result<User>.Fail("E-mail ou senha inválidos.");

        var verification = hasher.Verify(user.PasswordHash, password);
        if (verification is PasswordVerification.Failed)
            return Result<User>.Fail("E-mail ou senha inválidos.");

        // O algoritmo/parâmetros mudaram desde que a senha foi gravada: regrava
        // com o formato atual, aproveitando que temos a senha em mãos agora.
        if (verification is PasswordVerification.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.Hash(password);
            await users.UpdateAsync(user, ct);
        }

        user.LastSeenAt = DateTimeOffset.UtcNow;
        await users.UpdateAsync(user, ct);

        return Result<User>.Success(user);
    }
}
