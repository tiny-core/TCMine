using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using Microsoft.Identity.Client;
using TCMine_Application.Launcher;
using TCMine_Domain.Launcher;
using TCMine_Launcher.Infrastructure.Configuration;
using XboxAuthNet.Game.Msal;

namespace TCMine_Launcher.Infrastructure.Auth;

/// <summary>
///     Login Microsoft/Xbox no próprio launcher via CmlLib + MSAL. Implementa a porta
///     <see cref="IAuthService" /> (devolve um <see cref="PlayerSession" /> de domínio) e expõe a
///     <see cref="MSession" /> do CmlLib ao orquestrador (ambos na infraestrutura) para o lançamento.
///     No Windows o MSAL usa WebView2 (popup) para o login interativo e o cache DPAPI para o silencioso.
///     O Azure client id vem embutido no Build (<see cref="AppConfig.MicrosoftClientId" />).
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly JELoginHandler _handler = JELoginHandlerBuilder.BuildDefault();
    private IPublicClientApplication? _app;

    /// <summary>Sessão Minecraft do CmlLib (token + perfil) — usada pelo orquestrador do launch.</summary>
    internal MSession? CurrentMSession { get; private set; }

    public PlayerSession? Current =>
        CurrentMSession is { } s ? new PlayerSession(s.UUID ?? "", s.Username ?? "") : null;

    public async Task<PlayerSession?> TryLoginSilentAsync(CancellationToken ct = default)
    {
        try
        {
            var app = await GetAppAsync();
            var auth = _handler.CreateAuthenticatorWithDefaultAccount(ct);
            auth.AddMsalOAuth(app, msal => msal.Silent());
            auth.AddXboxAuthForJE(xb => xb.Basic());
            auth.AddJEAuthenticator();
            CurrentMSession = await auth.ExecuteForLauncherAsync();
            return Current;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null; // sem conta/token em cache → não autenticado
        }
    }

    public async Task<PlayerSession> LoginAsync(CancellationToken ct = default)
    {
        if (await TryLoginSilentAsync(ct) is { } silent) return silent;

        var app = await GetAppAsync();
        var auth = _handler.CreateAuthenticatorWithNewAccount(ct);
        auth.AddMsalOAuth(app, msal => msal.Interactive());
        auth.AddXboxAuthForJE(xb => xb.Basic());
        auth.AddJEAuthenticator();
        CurrentMSession = await auth.ExecuteForLauncherAsync();
        return Current!;
    }

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

        CurrentMSession = null;
    }

    private async Task<IPublicClientApplication> GetAppAsync()
    {
        var clientId = AppConfig.MicrosoftClientId;
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException(
                "Azure client id não configurado. Defina MicrosoftClientId no build " +
                "(Client.props ou -p:MicrosoftClientId=…).");

        return _app ??= await MsalClientHelper.BuildApplicationWithCache(clientId);
    }
}