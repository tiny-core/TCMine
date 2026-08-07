using System.Security.Cryptography;
using System.Text;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Security;

/// <summary>
///     Inicia a recuperação de senha: gera um token de uso único, guarda só o
///     hash dele e manda o link por e-mail.
/// </summary>
public sealed class RequestPasswordReset(IUserRepository users, IEmailSender email)
{
    /// <summary>Janela curta de propósito: link de reset é chave temporária da conta.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    public async Task<Result> HandleAsync(string emailAddress, string resetUrlTemplate, CancellationToken ct)
    {
        var user = await users.GetByEmailAsync(emailAddress.Trim(), ct);

        // Sucesso mesmo quando o e-mail não existe: responder "não encontrado"
        // transformaria esta tela num verificador de quem tem conta aqui.
        if (user is null)
            return Result.Success();

        var token = GenerateToken();
        user.PasswordResetTokenHash = HashToken(token);
        user.PasswordResetTokenExpiresAt = DateTimeOffset.UtcNow.Add(Lifetime);
        await users.UpdateAsync(user, ct);

        var link = resetUrlTemplate.Replace("{token}", Uri.EscapeDataString(token));

        await email.SendAsync(
            user.Email,
            "Recuperação de senha — TCMine",
            $"""
             Olá, {user.DisplayName}.

             Recebemos um pedido para redefinir a sua senha do painel TCMine.
             Abra o link abaixo para escolher uma nova senha:

             {link}

             O link vale por {Lifetime.TotalHours:0} hora e só pode ser usado uma vez.
             Se não foi você quem pediu, ignore este e-mail: nada muda.
             """,
            ct);

        return Result.Success();
    }

    /// <summary>256 bits de aleatoriedade criptográfica, em base64 seguro para URL.</summary>
    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>
    ///     SHA-256 direto (sem salt, sem custo alto de propósito): o token já é
    ///     aleatório de 256 bits, então não há dicionário a resistir — só
    ///     precisamos que o valor guardado não sirva como link.
    /// </summary>
    internal static string HashToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(hash);
    }
}
