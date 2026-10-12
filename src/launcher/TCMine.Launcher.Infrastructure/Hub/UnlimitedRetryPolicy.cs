using Microsoft.AspNetCore.SignalR.Client;
using TCMine.Launcher.Core.Connectivity;

namespace TCMine.Launcher.Infrastructure.Hub;

/// <summary>
///     Política de reconexão do SignalR que nunca desiste por cansaço.
///     Devolver nulo é como se diz "chega" ao SignalR, e por falha de rede esta
///     nunca devolve: a espera vem do <see cref="ReconnectBackoff" />, que tem
///     teto.
///     A única desistência é a sessão recusada. Insistir aí seria bater para
///     sempre numa porta que só abre com outra chave — e esconderia do resto do
///     launcher que é preciso entrar de novo.
/// </summary>
internal sealed class UnlimitedRetryPolicy : IRetryPolicy
{
    /// <summary>A última tentativa de religar foi recusada por falta de sessão.</summary>
    public bool SessionRejected { get; private set; }

    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        if (HubErrors.IsUnauthorized(retryContext.RetryReason))
        {
            SessionRejected = true;

            return null;
        }

        return ReconnectBackoff.DelayFor(retryContext.PreviousRetryCount);
    }
}
