using System.Net;
using Microsoft.Extensions.Logging;

namespace TCMine.Launcher.Infrastructure.Hub;

/// <summary>
///     Cria o cliente do Hub sob demanda.
///     Não dá para registrar LauncherHubClient direto no DI: ele precisa da URL
///     do servidor, que só existe depois de o pareamento ter sido lido. A factory
///     adia a construção até esse momento.
/// </summary>
public sealed class LauncherHubClientFactory(ILoggerFactory loggerFactory, CookieContainer cookies)
{
    public LauncherHubClient Create(Uri serverUrl) =>
        new(serverUrl, cookies, loggerFactory.CreateLogger<LauncherHubClient>());
}
