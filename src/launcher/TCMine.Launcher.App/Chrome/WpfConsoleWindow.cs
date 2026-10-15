using System.Windows;
using TCMine.Launcher.UI.Abstractions;

namespace TCMine.Launcher.App.Chrome;

/// <summary>
///     Cria a janela do console na primeira vez que é pedida, e depois só a
///     mostra. Preguiçosa porque a maioria das sessões nunca a abre, e um
///     WebView2 a mais no arranque custaria memória e tempo a toda a gente.
/// </summary>
internal sealed class WpfConsoleWindow(IServiceProvider services) : IConsoleWindow
{
    private ConsoleWindow? _window;

    public void Show() => Application.Current.Dispatcher.Invoke(() =>
    {
        if (_window is null)
        {
            var window = new ConsoleWindow(services);
            _window = window;

            // Escondida não conta como fechada: sem isto, fechar a principal
            // deixaria o console vivo, e o processo com ele.
            if (Application.Current.MainWindow is { } main)
                main.Closed += (_, _) => window.CloseForGood();
        }

        if (_window.WindowState is WindowState.Minimized)
            _window.WindowState = WindowState.Normal;

        _window.Show();
        _window.Activate();
    });
}
