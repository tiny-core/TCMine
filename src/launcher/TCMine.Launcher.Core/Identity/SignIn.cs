using TCMine.Contracts;
using TCMine.Contracts.Identity;
using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Core.Identity;

/// <summary>
///     Entra no servidor com a conta Minecraft do jogador.
///     São dois passos que só valem juntos: provar à Microsoft que a conta é
///     dele, e trocar essa prova por uma sessão no TCMine Server. O token do
///     Minecraft não sobrevive a este método — quem vale daqui em diante é o
///     cookie que o servidor devolveu.
/// </summary>
public sealed class SignIn(IMinecraftAuthenticator authenticator, ILauncherSessionApi api)
{
    /// <summary>
    ///     Tenta entrar sem incomodar o jogador, com o que já estiver guardado.
    ///     Roda no arranque. Não ter credencial guardada é o caso normal do
    ///     primeiro uso, e por isso volta silencioso: uma mensagem de erro na
    ///     abertura ensinaria o jogador a ignorar mensagens de erro.
    /// </summary>
    public async Task<SignInState> ResumeAsync(LauncherConfig config, CancellationToken ct)
    {
        var auth = await authenticator.TrySilentAsync(config.AzureClientId, ct);

        if (auth.Outcome is not MinecraftAuthOutcome.Success)
            return SignInState.SignedOut();

        return await TrocarPorSessaoAsync(config.ServerUrl, auth.AccessToken!, ct);
    }

    /// <summary>Abre o fluxo interativo. Vem de um clique, então pode falar.</summary>
    public async Task<SignInState> InteractiveAsync(LauncherConfig config, CancellationToken ct)
    {
        var auth = await authenticator.SignInAsync(config.AzureClientId, ct);

        switch (auth.Outcome)
        {
            case MinecraftAuthOutcome.Success:
                return await TrocarPorSessaoAsync(config.ServerUrl, auth.AccessToken!, ct);

            // Fechar a janela do navegador é uma decisão, não uma falha. Avisar
            // seria repetir ao jogador o que ele acabou de fazer.
            case MinecraftAuthOutcome.Cancelled:
                return SignInState.SignedOut();

            default:
                return SignInState.Failed(auth.Message ?? "Não foi possível autenticar com a Microsoft.");
        }
    }

    /// <summary>
    ///     Sai dos dois lados. O servidor primeiro: se a ordem fosse a inversa e
    ///     a rede caísse no meio, a máquina ficaria sem credencial local e com a
    ///     sessão viva do outro lado — sem como encerrá-la.
    /// </summary>
    public async Task<SignInState> SignOutAsync(LauncherConfig config, CancellationToken ct)
    {
        await api.SignOutAsync(config.ServerUrl, ct);
        await authenticator.SignOutAsync(ct);

        return SignInState.SignedOut();
    }

    private async Task<SignInState> TrocarPorSessaoAsync(Uri serverUrl, string accessToken, CancellationToken ct)
    {
        var sessao = await api.SignInAsync(serverUrl, accessToken, ct);

        return sessao.Outcome switch
        {
            SessionOutcome.Success => SignInState.SignedIn(sessao.Session!),
            SessionOutcome.Rejected => SignInState.Rejected(sessao.Message!),
            _ => SignInState.Failed(sessao.Message!)
        };
    }
}

/// <summary>
///     Se há sessão neste servidor, e o que dizer quando não há.
/// </summary>
public sealed record SignInState
{
    public required SignInStatus Status { get; init; }

    public LauncherSessionDto? Session { get; init; }

    public string? Message { get; init; }

    public bool IsSignedIn => Status is SignInStatus.SignedIn;

    public static SignInState SignedOut() => new() { Status = SignInStatus.SignedOut };

    public static SignInState SignedIn(LauncherSessionDto session) =>
        new() { Status = SignInStatus.SignedIn, Session = session };

    public static SignInState Rejected(string message) =>
        new() { Status = SignInStatus.Rejected, Message = message };

    public static SignInState Failed(string message) =>
        new() { Status = SignInStatus.Failed, Message = message };
}

public enum SignInStatus
{
    SignedOut,
    SignedIn,

    /// <summary>O servidor não aceitou a conta. Tentar de novo não resolve.</summary>
    Rejected,

    /// <summary>Rede, servidor fora do ar, Microsoft indisponível.</summary>
    Failed
}
