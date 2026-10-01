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
using TCMine.Launcher.Infrastructure.Game;
using TCMine.Launcher.Infrastructure.Runtime;
using TCMine.Launcher.Infrastructure.Updates;
using TCMine.Launcher.Infrastructure.Identity;
using TCMine.MinecraftAuth;

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

        // Substituído pela implementação do MSAL na fatia seguinte. É o único
        // degrau que falta: a cadeia depois dele já existe abaixo.
        services.AddSingleton<IMicrosoftTokenProvider, PendingMicrosoftTokenProvider>();

        // Cliente PRÓPRIO, sem o CookieContainer partilhado: a troca com a Xbox
        // Live, o XSTS e o Minecraft Services é com a Microsoft, e mandar para
        // eles o cookie de sessão do TCMine seria entregar a sessão do jogador
        // a quem não tem nada com ela.
        services.AddHttpClient<MinecraftTokenExchange>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddStandardResilienceHandler();

        services.AddTransient<IMinecraftAuthenticator, MinecraftAuthenticator>();

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

        // Sem timeout global, como nos blobs: um JRE são dezenas de megabytes e
        // o cancelamento correto é o do jogador, pelo CancellationToken.
        services.AddHttpClient<IJavaLocator, AdoptiumJavaLocator>(client =>
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
        });

        services.AddHttpClient<IJavaRequirementSource, MojangJavaRequirementSource>(client =>
        {
            // Dois JSON pequenos. Demorar aqui atrasaria o arranque do jogo, e o
            // palpite serve enquanto isso.
            client.Timeout = TimeSpan.FromSeconds(20);
        });

        // Cliente próprio e sem timeout: instalar um loader baixa dezenas de
        // megabytes de bibliotecas, e o cancelamento certo é o do jogador.
        services.AddHttpClient<IGameLauncher, CmlLibGameLauncher>(client =>
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
        });

        // Sem hardlink por padrão: o host de Windows substitui. Ver NoFileLinker.
        services.AddSingleton<IFileLinker, NoFileLinker>();
        services.AddSingleton<IContentStore, FileSystemContentStore>();
        services.AddSingleton<IInstanceStore, FileSystemInstanceStore>();
        services.AddSingleton<IWorldBackup, ZipWorldBackup>();
        services.AddSingleton<ILauncherUpdater, VelopackLauncherUpdater>();
        services.AddSingleton<IActiveInstanceStore, FileActiveInstanceStore>();
        services.AddSingleton<IPlayerProfileCache, FilePlayerProfileCache>();

        services.AddHttpClient<IPlayerProfileSource, MinecraftServicesProfileSource>(client =>
        {
            // Um JSON minúsculo, e está no caminho de abrir o jogo: demorar aqui
            // faria o jogador esperar por um nome que o cache já sabe.
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddSingleton<LauncherHubClientFactory>();

        // Singleton: uma conexão para a aplicação inteira. Ver
        // SignalRServerConnection para o porquê de não haver uma por tela.
        services.AddSingleton<IServerConnection, SignalRServerConnection>();

        return services;
    }
}
