using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Security;

/// <summary>
///     Troca a senha do próprio usuário, exigindo a senha atual.
///     Pedir a atual não é burocracia: impede que alguém com a sessão aberta
///     (máquina destravada, cookie roubado) tome a conta trocando a senha.
/// </summary>
public sealed class ChangePassword(IUserRepository users, IPasswordHasher hasher)
{
    public async Task<Result> HandleAsync(
        Guid userId, string currentPassword, string newPassword, CancellationToken ct)
    {
        if (newPassword.Length < CreateFirstAdmin.MinPasswordLength)
            return Result.Fail($"A nova senha precisa de pelo menos {CreateFirstAdmin.MinPasswordLength} caracteres.");

        var user = await users.GetByIdAsync(userId, ct);
        if (user is null)
            return Result.Fail("Usuário não encontrado.");

        // Conta sem senha é conta só-Microsoft: não há "senha atual" para conferir.
        if (user.PasswordHash is null)
            return Result.Fail("Esta conta não usa senha local.");

        if (hasher.Verify(user.PasswordHash, currentPassword) is PasswordVerification.Failed)
            return Result.Fail("Senha atual incorreta.");

        user.PasswordHash = hasher.Hash(newPassword);

        // Qualquer link de recuperação em aberto morre aqui: quem trocou a senha
        // sabendo a atual não precisa dele, e deixá-lo vivo seria uma porta extra.
        user.PasswordResetTokenHash = null;
        user.PasswordResetTokenExpiresAt = null;

        await users.UpdateAsync(user, ct);
        return Result.Success();
    }
}
