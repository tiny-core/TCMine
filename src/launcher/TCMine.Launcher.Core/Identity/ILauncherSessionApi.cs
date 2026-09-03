using TCMine.Contracts.Identity;

namespace TCMine.Launcher.Core.Identity;

/// <summary>
///     A conversa de sessão com o TCMine Server.
///     O token do Minecraft entra aqui e não sai: o servidor o troca por um
///     cookie de sessão, e é esse cookie — o mesmo do painel — que autentica o
///     hub e os downloads daí em diante. Guardar o cookie é responsabilidade da
///     implementação, porque quem sabe o que é um cookie é o transporte.
/// </summary>
public interface ILauncherSessionApi
{
    Task<SessionResult> SignInAsync(Uri serverUrl, string minecraftAccessToken, CancellationToken ct);

    Task SignOutAsync(Uri serverUrl, CancellationToken ct);
}

public sealed record SessionResult(
    SessionOutcome Outcome,
    LauncherSessionDto? Session,
    string? Message)
{
    public static SessionResult Success(LauncherSessionDto session) =>
        new(SessionOutcome.Success, session, null);

    public static SessionResult Rejected(string message) =>
        new(SessionOutcome.Rejected, null, message);

    public static SessionResult Failed(string message) =>
        new(SessionOutcome.Failed, null, message);
}

public enum SessionOutcome
{
    Success,

    /// <summary>
    ///     O servidor recusou a credencial (401). Repetir não adianta — ou o
    ///     token expirou, ou a conta não serve. Distinto de
    ///     <see cref="Failed" />, que é problema de rede e pede nova tentativa.
    /// </summary>
    Rejected,

    Failed
}
