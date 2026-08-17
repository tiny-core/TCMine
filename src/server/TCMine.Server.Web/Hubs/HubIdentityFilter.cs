using Microsoft.AspNetCore.SignalR;
using TCMine.Server.Web.Security;

namespace TCMine.Server.Web.Hubs;

/// <summary>
///     Deposita o usuário da conexão no <see cref="UserPrincipalHolder" /> antes
///     de cada invocação de hub.
///     Filtro, e não uma chamada no início de cada método do hub, porque método
///     novo esqueceria — e o sintoma do esquecimento seria "servidor não
///     encontrado" para um usuário legítimo, que ninguém liga a autorização.
///     O SignalR cria um escopo de DI por invocação, e o filtro roda dentro
///     dele: qualquer caso de uso resolvido ali enxerga a mesma identidade.
/// </summary>
public sealed class HubIdentityFilter : IHubFilter
{
    public ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        Seed(invocationContext.ServiceProvider, invocationContext.Context);
        return next(invocationContext);
    }

    public Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        Seed(context.ServiceProvider, context.Context);
        return next(context);
    }

    public Task OnDisconnectedAsync(
        HubLifetimeContext context,
        Exception? exception,
        Func<HubLifetimeContext, Exception?, Task> next)
    {
        Seed(context.ServiceProvider, context.Context);
        return next(context, exception);
    }

    private static void Seed(IServiceProvider services, HubCallerContext caller) =>
        services.GetRequiredService<UserPrincipalHolder>().Set(caller.User);
}
