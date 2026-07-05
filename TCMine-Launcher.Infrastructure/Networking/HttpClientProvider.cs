using System.Net.Http;

namespace TCMine_Launcher.Infrastructure.Networking;

/// <summary>
/// <see cref="HttpClient"/> partilhado para downloads (jars/overrides/imagens). Timeout infinito
/// (ficheiros grandes); o cert self-signed de dev é aceite só em DEBUG.
/// </summary>
public static class HttpClientProvider
{
    public static HttpClient Shared { get; } = Create();

    private static HttpClient Create()
    {
        var handler = new HttpClientHandler();
#if DEBUG
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
#endif
        var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TCMine-Launcher/1.0");
        return client;
    }
}
