using System.Security.Cryptography;
using System.Text;
using TCMine.Server.Application.Security;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Cloud;

/// <summary>
///     Formato da chave de um servidor de jogo: <c>tcs_&lt;prefixo&gt;_&lt;segredo&gt;</c>.
///     O prefixo (12 caracteres, não secreto) acha a linha no banco sem varrer
///     hashes e identifica a chave no painel; o segredo (256 bits) é o que vale.
///     Guardamos só o hash da chave inteira (<see cref="SecureToken.Hash" />).
/// </summary>
public static class CloudServerKey
{
    private const string Scheme = "tcs";
    private const string PrefixAlphabet = "abcdefghijkmnpqrstuvwxyz23456789";

    /// <summary>Gera uma chave nova: (chave em claro, prefixo, hash).</summary>
    public static (string Key, string Prefix, string Hash) Generate()
    {
        var prefix = new char[CloudServerCredential.PrefixLength];
        for (var i = 0; i < prefix.Length; i++)
            prefix[i] = PrefixAlphabet[RandomNumberGenerator.GetInt32(PrefixAlphabet.Length)];

        // O segredo em base64url pode ter "_"; o parse corta só no SEGUNDO "_".
        var key = $"{Scheme}_{new string(prefix)}_{SecureToken.Generate()}";
        return (key, new string(prefix), SecureToken.Hash(key));
    }

    /// <summary>Prefixo de uma chave recebida, ou nulo se o formato não é o nosso.</summary>
    public static string? PrefixOf(string? key)
    {
        if (string.IsNullOrEmpty(key) || key.Length > 128) return null;
        var parts = key.Split('_', 3);
        if (parts.Length != 3 || parts[0] != Scheme || parts[1].Length != CloudServerCredential.PrefixLength
            || parts[2].Length == 0)
            return null;
        return parts[1];
    }

    /// <summary>
    ///     Compara em tempo constante: comparar hash com <c>==</c> vaza, pelo
    ///     tempo de resposta, quantos caracteres bateram.
    /// </summary>
    public static bool Matches(string key, string storedHash) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(SecureToken.Hash(key)),
            Encoding.ASCII.GetBytes(storedHash));
}
