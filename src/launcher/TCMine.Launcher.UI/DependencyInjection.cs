using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using TCMine.Launcher.UI.State;

namespace TCMine.Launcher.UI;

public static class DependencyInjection
{
    /// <summary>
    ///     Registra o que as telas precisam. O host acrescenta o que só ele sabe
    ///     construir — a implementação de <see cref="Abstractions.IWindowChrome" />
    ///     e o <see cref="Abstractions.LauncherAppInfo" /> da build.
    /// </summary>
    public static IServiceCollection AddLauncherUi(this IServiceCollection services)
    {
        // Singleton, não scoped: em Blazor Hybrid existe um circuito só, e o
        // estado tem de sobreviver à navegação entre páginas.
        services.AddSingleton<LauncherShellState>();
        services.AddSingleton<InstallOperationState>();
        services.AddSingleton<ActionLock>();
        services.AddSingleton<GameWindowCoordinator>();
        services.AddScoped<PostPairingRoute>();
        services.AddScoped<LinkSupervisor>();

        // Scoped como o supervisor: em Blazor Hybrid o escopo é a janela
        // inteira, então vivem tanto quanto a aplicação — e podem depender dos
        // casos de uso do Core, que são scoped.
        services.AddScoped<CatalogStore>();
        services.AddScoped<NewsStore>();

        services.AddMudServices();

        return services;
    }
}
