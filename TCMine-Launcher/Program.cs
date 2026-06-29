using System.Reflection;
using Avalonia;
using JetBrains.Annotations;
using ReactiveUI.Avalonia;
using TCMine_Launcher.ViewModels;

namespace TCMine_Launcher;

[UsedImplicitly]
internal sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    private static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI(rxAppBuilder =>
            {
                rxAppBuilder
                    .WithViewsFromAssembly(Assembly.GetExecutingAssembly())
                    .WithRegistration(locator => { locator.RegisterLazySingleton(() => new MainWindowViewModel()); });
            });
}