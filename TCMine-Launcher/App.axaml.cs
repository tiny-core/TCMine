using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Splat;
using TCMine_Launcher.ViewModels;
using TCMine_Launcher.Views;

namespace TCMine_Launcher;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow
            {
                DataContext = AppLocator.Current.GetService<MainWindowViewModel>()
            };

        base.OnFrameworkInitializationCompleted();
    }
}