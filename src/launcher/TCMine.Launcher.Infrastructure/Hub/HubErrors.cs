using System.Net;

namespace TCMine.Launcher.Infrastructure.Hub;

internal static class HubErrors
{
    /// <summary>
    ///     A falha foi o servidor a recusar a sessão (401)?
    ///     Percorre as causas porque o SignalR nem sempre entrega a exceção do
    ///     pedido HTTP diretamente — conforme o ponto da ligação em que falha,
    ///     ela chega embrulhada noutra.
    /// </summary>
    public static bool IsUnauthorized(Exception? error)
    {
        for (var current = error; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized })
                return true;
        }

        return false;
    }
}
