namespace TCMine.Launcher.Core.Abstractions;

/// <summary>
///     Um token, ou o motivo de não haver um.
///     É o mesmo vocabulário nos dois degraus da autenticação — provar a conta à
///     Microsoft e trocar essa prova por um token do Minecraft — porque os
///     desfechos são literalmente os mesmos: entrou, não havia credencial
///     guardada, o jogador desistiu, esta build não sabe entrar, ou falhou. Dois
///     tipos com a mesma forma só obrigariam a traduzir um no outro em cada
///     passagem, e é nessa tradução que um desfecho se perde.
///     O que o <see cref="AccessToken" /> significa depende de quem devolveu:
///     <see cref="IMicrosoftTokenProvider" /> entrega o token da Microsoft;
///     <see cref="IMinecraftAuthenticator" />, o do Minecraft. Nenhum dos dois é
///     guardado pelo launcher.
/// </summary>
public sealed record AuthResult(
    AuthOutcome Outcome,
    string? AccessToken,
    string? Message)
{
    public static AuthResult Success(string accessToken) =>
        new(AuthOutcome.Success, accessToken, null);

    public static AuthResult NoStoredCredentials() =>
        new(AuthOutcome.NoStoredCredentials, null, null);

    public static AuthResult Cancelled() =>
        new(AuthOutcome.Cancelled, null, null);

    public static AuthResult Unavailable(string message) =>
        new(AuthOutcome.Unavailable, null, message);

    public static AuthResult Failed(string message) =>
        new(AuthOutcome.Failed, null, message);
}

public enum AuthOutcome
{
    Success,

    /// <summary>Nada guardado. O primeiro arranque de toda instalação.</summary>
    NoStoredCredentials,

    /// <summary>O jogador fechou a janela do navegador. Não é erro.</summary>
    Cancelled,

    /// <summary>Esta build não sabe autenticar (ainda). Distinto de falhar.</summary>
    Unavailable,

    Failed
}
