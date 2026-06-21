using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using TCMine_Application.Identity;

namespace TCMine_Server.Authentication;

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
public sealed class PersistingAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    private const string PersistKey = "tcmine.auth.user";

    private readonly PersistentComponentState _state;
    private readonly PersistingComponentStateSubscription _subscription;
    private readonly Task<AuthenticationState> _authStateTask;

    /// <summary>
    /// Uma implementação personalizada de <see cref="AuthenticationStateProvider"/>
    /// que persiste o estado de autenticação em pre-renderização do servidor e circuitos interativos em aplicativos Blazor Server.
    /// </summary>
    public PersistingAuthenticationStateProvider(
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

    /// <summary>
    /// Recupera o estado de autenticação atual para componentes Blazor.
    /// Esta implementação verifica a identidade persistida, garantindo que o estado de autorização
    /// seja restaurado de forma transparente entre a pré-renderização no servidor e os circuitos interativos
    /// em aplicações Blazor Server.ações Blazor Server.
    /// </summary>
    /// <returns>Uma <see cref="Task{TResult}"/> representando a operação assíncrona,
    /// com um resultado do <see cref="AuthenticationState"/> atual.
    /// </returns>
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
        _subscription.Dispose();
    }

    private static ClaimsPrincipal Anonymous => new(new ClaimsIdentity());
}
