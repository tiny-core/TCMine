using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Infrastructure.Windows.Identity;

namespace TCMine.Launcher.Infrastructure.Windows;

/// <summary>
///     O que só o Windows sabe fazer, num registo só.
///     Tem de ser chamado DEPOIS de <c>AddLauncherInfrastructure</c>: as duas
///     portas abaixo têm implementação portável registada lá — uma que copia em
///     vez de ligar, outra que diz não saber entrar — e aqui o último registo
///     vence. A ordem invertida deixaria o launcher a copiar mods e sem login,
///     compilando e passando nos testes.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddWindowsLauncherInfrastructure(
        this IServiceCollection services,
        string rootDirectory)
    {
        // Quem tem janela é o host, e ele regista a implementação: aqui só se
        // declara que o broker precisa de uma.
        // Hardlink: é o que faz dez modpacks com o mesmo mod ocuparem um ficheiro.
        services.AddSingleton<IFileLinker, WindowsFileLinker>();

        // Singleton porque o cache do MSAL é do processo: uma instância por
        // pedido reabriria e voltaria a decifrar o ficheiro a cada ecrã.
        services.AddSingleton<IMicrosoftTokenProvider>(sp =>
            new MsalMicrosoftTokenProvider(
                Path.Combine(rootDirectory, "identity"),
                sp.GetRequiredService<IParentWindowHandle>(),
                sp.GetRequiredService<ILogger<MsalMicrosoftTokenProvider>>()));

        return services;
    }
}
