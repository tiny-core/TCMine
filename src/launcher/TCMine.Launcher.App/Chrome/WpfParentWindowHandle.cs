using System.Windows.Interop;
using TCMine.Launcher.Infrastructure.Windows;

namespace TCMine.Launcher.App.Chrome;

/// <summary>
///     O HWND da janela principal, para o broker do Windows desenhar sobre ela.
///     Lê a cada chamada em vez de guardar: o handle só existe depois de a
///     janela ser mostrada, e o login silencioso do arranque corre antes disso.
///     Guardá-lo no construtor devolveria zero para sempre.
/// </summary>
internal sealed class WpfParentWindowHandle(MainWindow window) : IParentWindowHandle
{
    public nint Handle => window.Dispatcher.Invoke(() => new WindowInteropHelper(window).Handle);
}
