using Microsoft.Extensions.DependencyInjection;
using TCMine.Launcher.Core.Connectivity;
using TCMine.Launcher.Infrastructure.Configuration;
using TCMine.Launcher.Infrastructure.Connectivity;
using TCMine.Launcher.Infrastructure.Hub;

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

        services.AddSingleton<LauncherHubClientFactory>();

        return services;
    }
}