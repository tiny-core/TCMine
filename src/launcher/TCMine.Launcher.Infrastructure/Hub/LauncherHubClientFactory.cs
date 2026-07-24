using Microsoft.Extensions.Logging;

namespace TCMine.Launcher.Infrastructure.Hub;

/// <summary>
///     Cria o cliente do Hub sob demanda.
///     Não dá para registrar LauncherHubClient direto no DI: ele precisa da URL
///     do servidor e do provedor de token, que só existem depois que o config
///     foi lido e o login aconteceu. A factory adia a construção até esse momento.
/// </summary>
public sealed class LauncherHubClientFactory(ILoggerFactory loggerFactory)
{
    public LauncherHubClient Create(Uri serverUrl, Func<Task<string?>> accessTokenProvider)
    {
        return new LauncherHubClient(serverUrl, accessTokenProvider, loggerFactory.CreateLogger<LauncherHubClient>());
    }
}