using System.Net;
using Microsoft.Extensions.DependencyInjection;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Connectivity;
using TCMine.Launcher.Core.Identity;
using TCMine.Launcher.Infrastructure.Configuration;
using TCMine.Launcher.Infrastructure.Connectivity;
using TCMine.Launcher.Infrastructure.Content;
using TCMine.Launcher.Infrastructure.Hub;
using TCMine.Launcher.Infrastructure.Instances;
using TCMine.Launcher.Infrastructure.Identity;

namespace TCMine.Launcher.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLauncherInfrastructure(
        this IServiceCollection services,
        string rootDirectory)
    {
        services.AddSingleton(new LauncherPaths(rootDirectory));

        services.AddSingleton<ILauncherConfigProvider, FileLauncherConfigProvider>();

        services.AddHttpClient<IHandshakeClient, HandshakeClient>(client =>
            {
                // Timeout curto: o handshake é uma resposta pequena, e o
                // jogador está olhando para uma tela de carregamento.
                client.Timeout = TimeSpan.FromSeconds(15);
            })
            // Retry com backoff, circuit breaker e timeout por tentativa. O
            // servidor pode estar reiniciando após um update — vale
            // tentar de novo antes de dizer que está fora do ar.
            .AddStandardResilienceHandler();

        // UM pote de cookies para toda a aplicação. É ele que carrega a sessão
        // emitida no login para as chamadas seguintes — e, na próxima fatia,
        // para o hub e para os downloads. Um container por HttpClient faria o
        // jogador entrar e, no pedido seguinte, ser tratado como anônimo.
        services.AddSingleton<CookieContainer>();

        services.AddHttpClient<ILauncherSessionApi, LauncherSessionApi>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .ConfigurePrimaryHttpMessageHandler(sp => new HttpClientHandler
            {
                CookieContainer = sp.GetRequiredService<CookieContainer>(), UseCookies = true
            })
            .AddStandardResilienceHandler();

        // Substituído pela implementação real do MSAL na fatia da autenticação.
        // Registrado desde já para a tela de login existir sem quebrar o DI.
        services.AddSingleton<IMinecraftAuthenticator, PendingMinecraftAuthenticator>();

        services.AddHttpClient<IBlobDownloader, HttpBlobDownloader>(client =>
            {
                // Sem timeout global: um mod de duzentos megabytes numa ligação
                // ruim passa de qualquer prazo razoável, e o cancelamento correto
                // é o do jogador, pelo CancellationToken.
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .ConfigurePrimaryHttpMessageHandler(sp => new HttpClientHandler
            {
                CookieContainer = sp.GetRequiredService<CookieContainer>(), UseCookies = true
            });

        // Sem hardlink por padrão: o host de Windows substitui. Ver NoFileLinker.
        services.AddSingleton<IFileLinker, NoFileLinker>();
        services.AddSingleton<IContentStore, FileSystemContentStore>();
        services.AddSingleton<IInstanceStore, FileSystemInstanceStore>();

        services.AddSingleton<LauncherHubClientFactory>();

        // Singleton: uma conexão para a aplicação inteira. Ver
        // SignalRServerConnection para o porquê de não haver uma por tela.
        services.AddSingleton<IServerConnection, SignalRServerConnection>();

        return services;
    }
}
