using System.Security.Cryptography;
using System.Text;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Security;

/// <summary>
///     Conclui a recuperação: valida o token e grava a nova senha.
/// </summary>
public sealed class ResetPassword(IUserRepository users, IPasswordHasher hasher)
{
    public async Task<Result> HandleAsync(
        string emailAddress, string token, string newPassword, CancellationToken ct)
    {
        if (newPassword.Length < CreateFirstAdmin.MinPasswordLength)
            return Result.Fail($"A senha precisa de pelo menos {CreateFirstAdmin.MinPasswordLength} caracteres.");

        var user = await users.GetByEmailAsync(emailAddress.Trim(), ct);

        if (user?.PasswordResetTokenHash is null || user.PasswordResetTokenExpiresAt is null)
            return Result.Fail("Link inválido ou expirado. Peça a recuperação de novo.");

        if (user.PasswordResetTokenExpiresAt < DateTimeOffset.UtcNow)
            return Result.Fail("Link inválido ou expirado. Peça a recuperação de novo.");

        // Comparação em tempo constante: comparar strings com == vaza, pelo tempo
        // de resposta, quantos caracteres iniciais o palpite acertou.
        var presented = SecureToken.Hash(token);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(presented),
                Encoding.UTF8.GetBytes(user.PasswordResetTokenHash)))
            return Result.Fail("Link inválido ou expirado. Peça a recuperação de novo.");

        user.PasswordHash = hasher.Hash(newPassword);

        // Uso único: consome o token, senão o mesmo link redefiniria a senha de novo.
        user.PasswordResetTokenHash = null;
        user.PasswordResetTokenExpiresAt = null;

        await users.UpdateAsync(user, ct);
        return Result.Success();
    }
}
