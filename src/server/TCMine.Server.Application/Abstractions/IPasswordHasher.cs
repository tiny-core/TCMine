namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Hash de senha. A Application não escolhe o algoritmo — só declara que
///     precisa de um; a Infrastructure liga isso ao PBKDF2 do ASP.NET Core, que
///     é vetado e versionado (nada de criptografia caseira aqui).
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    ///     Confere a senha contra o hash. Devolve também se o hash está num
    ///     formato antigo e vale a pena regravar com os parâmetros atuais.
    /// </summary>
    PasswordVerification Verify(string hash, string password);
}

public enum PasswordVerification
{
    Failed,
    Success,
    SuccessRehashNeeded
}
