using Microsoft.Extensions.DependencyInjection;
using TCMine.Launcher.Core.Connectivity;
using TCMine.Launcher.Core.Identity;

namespace TCMine.Launcher.Core;

public static class DependencyInjection
{
    /// <summary>
    ///     Casos de uso do launcher. Só nomes daqui de dentro: quem implementa as
    ///     portas é registrado por <c>AddLauncherInfrastructure</c>, e esta camada
    ///     nunca vê o nome de uma classe de infraestrutura — a mesma regra do
    ///     <c>AddTCMineApplication</c> do lado do servidor.
    /// </summary>
    public static IServiceCollection AddLauncherCore(this IServiceCollection services)
    {
        services.AddScoped<ServerPairing>();
        services.AddScoped<SignIn>();

        return services;
    }
}
