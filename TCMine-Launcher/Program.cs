using System.Reflection;
using Avalonia;
using JetBrains.Annotations;
using ReactiveUI.Avalonia;
using Splat;
using TCMine_Launcher.Infrastructure;
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
                    .WithRegistration(locator =>
                    {
                        // Composição (root): instancia as implementações da infraestrutura e injeta
                        // as portas no ViewModel raiz. App pequeno — sem container pesado.
                        var config = new ServerConfig();
                        var auth = new AuthService();
                        var catalog = new ModpackCatalog(config);
                        var instanceStore = new InstanceStore();
                        var settingsStore = new SettingsStore();
                        var runState = new GameRunStateStore();
                        var pinger = new ServerPinger();
                        var systemInfo = new SystemInfo();
                        var orchestrator = new LaunchOrchestrator(auth, config);
                        var contentWatcher = new ContentWatcher(config);
                        var newsFeed = new NewsFeed(config);

                        locator.RegisterLazySingleton(() => new MainWindowViewModel(
                            auth, catalog, instanceStore, settingsStore, runState, orchestrator, pinger,
                            systemInfo, contentWatcher, newsFeed));
                    });
            });
}