using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TCMine_Launcher.ViewModels;
using TCMine_Launcher.Views;

namespace TCMine_Launcher;

/// <summary>
/// Representa o ponto de entrada da aplicação Avalonia.
/// Responsável por inicializar a aplicação, carregar XAML,
/// e configurar a janela principal da aplicação e o contexto de dados.
/// </summary>
public partial class App : Application
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
                DataContext = new MainWindowViewModel()
            };

        base.OnFrameworkInitializationCompleted();
    }
}