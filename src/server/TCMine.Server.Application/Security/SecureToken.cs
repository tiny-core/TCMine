using System.Security.Cryptography;
using System.Text;

namespace TCMine.Server.Application.Security;

/// <summary>
///     Segredos de uso único que saem do servidor e voltam: link de recuperação
///     de senha e código de convite.
///     Num lugar só porque a regra que importa é a mesma nos dois: guardamos o
///     HASH, nunca o valor. Um banco vazado não deve entregar links de reset
///     válidos nem convites de Owner prontos para usar.
/// </summary>
public static class SecureToken
{
    /// <summary>
    ///     Alfabeto sem os caracteres que se confundem ao ler em voz alta ou ao
    ///     digitar: I, L, O, U, 0 e 1 ficaram de fora. Um convite costuma ser
    ///     lido de uma mensagem e digitado à mão.
    /// </summary>
    private const string Alfabeto = "23456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>Token longo para links (256 bits, base64url).</summary>
    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>
    ///     Código curto para digitar, no formato XXXX-XXXX-XXXX-XXXX.
    ///     16 caracteres num alfabeto de 30 dão cerca de 78 bits — folgado para
    ///     um segredo que expira em dias e ainda passa por limite de taxa no
    ///     resgate. O token de 256 bits seria seguro também, mas ninguém digita
    ///     43 caracteres de base64 sem errar.
    /// </summary>
    public static string GenerateCode()
    {
        // 16 caracteres mais 3 hífens.
        var chars = new char[19];

        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = i is 4 or 9 or 14
                ? '-'
                : Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)];
        }

        return new string(chars);
    }

    /// <summary>
    ///     SHA-256 direto (sem salt, sem custo alto de propósito): o valor já é
    ///     aleatório, então não há dicionário a resistir — só precisamos que o
    ///     que está gravado não sirva como credencial.
    /// </summary>
    public static string Hash(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    ///     Normaliza o que o usuário digitou: caixa e hífens são cosméticos, e
    ///     recusar "abcd efgh" por causa de um espaço só gera suporte.
    /// </summary>
    public static string NormalizeCode(string code) =>
        new(code.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
