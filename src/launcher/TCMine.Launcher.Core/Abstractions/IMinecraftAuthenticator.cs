namespace TCMine.Launcher.Core.Abstractions;

/// <summary>
///     A cadeia Microsoft → Xbox Live → XSTS → Minecraft, na máquina do jogador.
///     Está atrás de porta por dois motivos. O primeiro é portabilidade: a
///     variante do MSAL com broker é Windows-only, e referenciá-la aqui
///     transformaria o port para Linux numa reescrita. O segundo é que o token
///     que sai daqui é a credencial mais sensível do launcher — deixá-la nascer
///     num único lugar, com contrato explícito, é mais barato de auditar do que
///     rastrear chamadas espalhadas.
///     O launcher NÃO guarda este token: ele serve uma vez, para provar
///     identidade ao servidor, e a sessão que vale a partir daí é o cookie que o
///     servidor devolve. O que se guarda é o refresh token da Microsoft, via
///     <see cref="ICredentialStore" />.
/// </summary>
public interface IMinecraftAuthenticator
{
    /// <summary>
    ///     Tenta reautenticar sem interação, a partir do que estiver guardado.
    ///     É o que faz o jogador abrir o launcher e já estar dentro. Não ter
    ///     credencial guardada é resultado normal, não erro.
    /// </summary>
    Task<MinecraftAuthResult> TrySilentAsync(string azureClientId, CancellationToken ct);

    /// <summary>Abre o fluxo interativo (navegador do sistema).</summary>
    Task<MinecraftAuthResult> SignInAsync(string azureClientId, CancellationToken ct);

    /// <summary>Descarta o que estiver guardado nesta máquina.</summary>
    Task SignOutAsync(CancellationToken ct);
}

public sealed record MinecraftAuthResult(
    MinecraftAuthOutcome Outcome,
    string? AccessToken,
    string? Message)
{
    public static MinecraftAuthResult Success(string accessToken) =>
        new(MinecraftAuthOutcome.Success, accessToken, null);

    public static MinecraftAuthResult NoStoredCredentials() =>
        new(MinecraftAuthOutcome.NoStoredCredentials, null, null);

    public static MinecraftAuthResult Cancelled() =>
        new(MinecraftAuthOutcome.Cancelled, null, null);

    public static MinecraftAuthResult Unavailable(string message) =>
        new(MinecraftAuthOutcome.Unavailable, null, message);

    public static MinecraftAuthResult Failed(string message) =>
        new(MinecraftAuthOutcome.Failed, null, message);
}

public enum MinecraftAuthOutcome
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
