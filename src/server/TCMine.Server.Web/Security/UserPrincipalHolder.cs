using System.Security.Claims;

namespace TCMine.Server.Web.Security;

/// <summary>
///     Quem é o usuário desta unidade de trabalho (requisição HTTP, circuito
///     Blazor ou invocação de hub).
///     Existe porque o <c>IHttpContextAccessor</c> sozinho responde diferente
///     conforme o transporte: numa conexão WebSocket a requisição de upgrade
///     continua viva e o contexto aparece; em long polling ela já terminou e o
///     <c>HttpContext</c> foi reciclado, então o accessor devolve nulo e o hub
///     deixa de reconhecer o usuário. Um teste de regressão trava os dois
///     transportes em <c>MainHubIdentidadeTests</c>.
///     A borda que conhece o principal o deposita aqui (o <see cref="Hubs.HubIdentityFilter" />
///     faz isso para o SignalR); o accessor fica como origem de último recurso,
///     que é onde ele é confiável — dentro de uma requisição HTTP em curso.
/// </summary>
public sealed class UserPrincipalHolder(IHttpContextAccessor accessor)
{
    private ClaimsPrincipal? _explicito;

    public ClaimsPrincipal? Current => _explicito ?? accessor.HttpContext?.User;

    /// <summary>
    ///     Fixa o principal desta unidade de trabalho. Chamar mais de uma vez com
    ///     valores diferentes seria sinal de escopo compartilhado entre usuários,
    ///     então o segundo valor vence só se o primeiro era nulo.
    /// </summary>
    public void Set(ClaimsPrincipal? principal) => _explicito ??= principal;
}
