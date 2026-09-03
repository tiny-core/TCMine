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

        services.AddMudServices();

        return services;
    }
}
