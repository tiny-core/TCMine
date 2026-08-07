using Microsoft.AspNetCore.Identity;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Infrastructure.Security;

/// <summary>
///     Liga a porta <see cref="IPasswordHasher" /> ao PasswordHasher do ASP.NET
///     Core (PBKDF2-HMAC-SHA256, com salt por senha e iterações versionadas).
///     Usar a implementação da plataforma em vez de escrever a nossa: quando as
///     iterações recomendadas subirem, vem de graça na atualização do pacote — e
///     o retorno "RehashNeeded" avisa para regravar o hash antigo.
/// </summary>
public sealed class AspNetPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _inner = new();

    private static User EmptyUser => new() { Email = "", DisplayName = "" };

    public string Hash(string password)
    {
        // O hasher aceita o usuário só para cenários de salt derivado; o padrão
        // não o usa, então passar um objeto vazio é seguro e evita acoplamento.
        return _inner.HashPassword(EmptyUser, password);
    }

    public PasswordVerification Verify(string hash, string password)
    {
        return _inner.VerifyHashedPassword(EmptyUser, hash, password) switch
        {
            PasswordVerificationResult.Success => PasswordVerification.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordVerification.SuccessRehashNeeded,
            _ => PasswordVerification.Failed
        };
    }
}
