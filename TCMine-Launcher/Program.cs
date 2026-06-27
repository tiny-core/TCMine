using Avalonia;
using ReactiveUI.Avalonia;
using System;

namespace TCMine_Launcher;

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
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace()
            // ReactiveUI.Avalonia 12.x mudou a assinatura: UseReactiveUI agora exige um builder
            // (configuração do ReactiveUI). Sem ajustes, passamos um builder vazio.
            .UseReactiveUI(_ => { });
}