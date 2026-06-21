using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using TCMine_Data.Authentication;

namespace TCMine_Data.Providers;

/// <summary>
/// Fonte do estado de autenticação para os componentes Blazor, baseada no cookie.
///
/// Substitui o antigo <c>CircuitAuthStateProvider</c> (estado em memória, perdido no F5).
/// Agora a identidade vem do cookie validado pelo middleware de autenticação:
/// <list type="bullet">
///   <item>No <b>prerender</b> (dentro do HttpContext) lê o <c>HttpContext.User</c> e o
///   persiste via <see cref="PersistentComponentState"/>;</item>
///   <item>No <b>circuito interativo</b> (onde já não há HttpContext) restaura a identidade
///   a partir do estado persistido — assim <c>&lt;AuthorizeView&gt;</c> funciona nas páginas
///   interativas (Dashboard, Configurações).</item>
/// </list>
/// Login/logout fazem navegação com reload completo (SSR), reiniciando o circuito e
/// relendo o cookie — por isso não é preciso revalidação contínua.
/// </summary>
public class PersistingAuthenticationState : AuthenticationStateProvider, IDisposable
{
    private const string PersistKey = "tcmine.auth.user";

    private readonly PersistentComponentState _state;
    private readonly PersistingComponentStateSubscription _subscription;
    private readonly Task<AuthenticationState> _authStateTask;

    public PersistingAuthenticationState(
        IHttpContextAccessor httpContextAccessor, PersistentComponentState state)
    {
        _state = state;

        var httpUser = httpContextAccessor.HttpContext?.User;
        if (httpUser?.Identity?.IsAuthenticated == true)
            // Prerender: identidade vem direto do cookie
            _authStateTask = Task.FromResult(new AuthenticationState(httpUser));
        else if (state.TryTakeFromJson<UserInfo>(PersistKey, out var info) && info is not null)
            // Circuito interativo: restaura o que o prerender persistiu
            _authStateTask = Task.FromResult(new AuthenticationState(info.ToPrincipal()));
        else
            // Sem cookie e sem estado: anônimo
            _authStateTask = Task.FromResult(new AuthenticationState(Anonymous));

        // Persiste a identidade no fim do prerender para o circuito a recuperar
        _subscription = state.RegisterOnPersisting(PersistAsync);
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return _authStateTask;
    }

    private async Task PersistAsync()
    {
        var authState = await _authStateTask;
        var principal = authState.User;
        if (principal.Identity?.IsAuthenticated == true)
            _state.PersistAsJson(PersistKey, UserInfo.FromPrincipal(principal));
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;

        _subscription.Dispose();
    }

    private static ClaimsPrincipal Anonymous => new(new ClaimsIdentity());
}