using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using Microsoft.Identity.Client;
using XboxAuthNet.Game.Msal;

namespace TCMine_Launcher.Services;

/// <summary>
/// Autenticação Microsoft/Xbox no próprio launcher via CmlLib + MSAL (modelo do backup). No Windows o
/// MSAL usa WebView2 (popup embutido) para o login interativo e o token em cache (DPAPI) para o login
/// silencioso — assim reentra entre execuções sem voltar a pedir credenciais. O servidor **não**
/// participa do login: só recebe o token Minecraft quando precisa (ex.: sync de configs do jogador).
///
/// O Azure client id vem embutido no build (<see cref="AppConfig.MicrosoftClientId"/>), nunca em
/// runtime — app desktop é um public client (sem secret).
/// </summary>
public sealed class AuthService
{
    private readonly JELoginHandler _handler = JELoginHandlerBuilder.BuildDefault();
    private IPublicClientApplication? _app;

    /// <summary>Sessão Minecraft atual (token + perfil), ou null se não autenticado.</summary>
    public MSession? Current { get; private set; }

    private async Task<IPublicClientApplication> GetAppAsync()
    {
        var clientId = AppConfig.MicrosoftClientId;
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException(
                "Azure client id não configurado. Defina MicrosoftClientId no build " +
                "(Client.props ou -p:MicrosoftClientId=…).");

        // BuildApplicationWithCache → WebView2 interativo + cache DPAPI persistente.
        return _app ??= await MsalClientHelper.BuildApplicationWithCache(clientId);
    }

    /// <summary>Login silencioso (token em cache). Devolve null se não há conta/token válido.</summary>
    public async Task<MSession?> TryLoginSilentAsync(CancellationToken ct = default)
    {
        try
        {
            var app = await GetAppAsync();
            var auth = _handler.CreateAuthenticatorWithDefaultAccount(ct);
            auth.AddMsalOAuth(app, msal => msal.Silent());
            auth.AddXboxAuthForJE(xb => xb.Basic());
            auth.AddJEAuthenticator();
            Current = await auth.ExecuteForLauncherAsync();
            return Current;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Sem conta/token em cache → tratado como não autenticado (segue para login interativo).
            return null;
        }
    }

    /// <summary>Login completo: tenta silencioso e, se preciso, abre o popup WebView2.</summary>
    public async Task<MSession> LoginAsync(CancellationToken ct = default)
    {
        if (await TryLoginSilentAsync(ct) is { } silent) return silent;

        var app = await GetAppAsync();
        var auth = _handler.CreateAuthenticatorWithNewAccount(ct);
        auth.AddMsalOAuth(app, msal => msal.Interactive());
        auth.AddXboxAuthForJE(xb => xb.Basic());
        auth.AddJEAuthenticator();
        Current = await auth.ExecuteForLauncherAsync();
        return Current;
    }

    /// <summary>Logout: remove a conta local e o token em cache do MSAL. Best-effort.</summary>
    public async Task SignOutAsync(CancellationToken ct = default)
    {
        try
        {
            var account = _handler.AccountManager.GetDefaultAccount();
            await _handler.Signout(account, ct);

            var app = await GetAppAsync();
            await MsalClientHelper.RemoveAccounts(app);
        }
        catch
        {
            // ignora falhas a limpar o cache MSAL
        }

        Current = null;
    }
}
