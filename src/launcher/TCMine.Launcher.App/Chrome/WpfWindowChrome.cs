using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using TCMine.Launcher.UI.Abstractions;

namespace TCMine.Launcher.App.Chrome;

/// <summary>
///     Liga a barra de título desenhada em Blazor à janela de verdade.
///     O caminho é o mesmo que o Windows usa internamente: ao clicar na barra,
///     soltamos a captura do mouse e mandamos à janela um WM_NCLBUTTONDOWN como
///     se o clique tivesse acontecido na legenda. Daí em diante quem move a
///     janela é o gestor de janelas — e é por isso que o snap às bordas
///     continua funcionando sem reimplementarmos nada.
///     <see cref="Window.DragMove" /> não serve aqui: exige estar dentro do
///     handler de mouse-down da WPF, e o nosso clique aconteceu dentro do
///     WebView2.
///     Sem detecção de duplo clique: a janela não maximiza (ResizeMode=
///     CanMinimize em MainWindow.xaml), então duplo clique na barra não tem o
///     que fazer — é só mais um arrasto, como qualquer outro clique.
/// </summary>
internal sealed partial class WpfWindowChrome(Window window) : IWindowChrome
{
    private const uint WmNcLButtonDown = 0x00A1;
    private const nint HtCaption = 2;

    public void BeginDrag() => window.Dispatcher.Invoke(() =>
    {
        var hwnd = new WindowInteropHelper(window).Handle;

        if (hwnd == nint.Zero)
            return;

        ReleaseCapture();
        SendMessage(hwnd, WmNcLButtonDown, HtCaption, nint.Zero);
    });

    public void Minimize() =>
        window.Dispatcher.Invoke(() => window.WindowState = WindowState.Minimized);

    public void Close() => window.Dispatcher.Invoke(window.Close);

    // ---------- P/Invoke ----------

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ReleaseCapture();

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    private static partial nint SendMessage(nint hWnd, uint msg, nint wParam, nint lParam);
}
