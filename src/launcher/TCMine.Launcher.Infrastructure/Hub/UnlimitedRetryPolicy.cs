using Microsoft.AspNetCore.SignalR.Client;
using TCMine.Launcher.Core.Connectivity;

namespace TCMine.Launcher.Infrastructure.Hub;

/// <summary>
///     Política de reconexão do SignalR que nunca desiste.
///     Devolver nulo é como se diz "chega" ao SignalR, e esta nunca devolve: a
///     espera vem do <see cref="ReconnectBackoff" />, que tem teto.
/// </summary>
internal sealed class UnlimitedRetryPolicy : IRetryPolicy
{
    public TimeSpan? NextRetryDelay(RetryContext retryContext) =>
        ReconnectBackoff.DelayFor(retryContext.PreviousRetryCount);
}
