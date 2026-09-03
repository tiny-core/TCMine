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
///     janela é o gestor de janelas — e é por isso que o snap às bordas, o
///     arrastar-para-maximizar e o restaurar-ao-arrastar continuam funcionando
///     sem reimplementarmos nada.
///     <see cref="Window.DragMove" /> não serve aqui: exige estar dentro do
///     handler de mouse-down da WPF, e o nosso clique aconteceu dentro do
///     WebView2.
/// </summary>
internal sealed partial class WpfWindowChrome : IWindowChrome
{
    private const uint WmNcLButtonDown = 0x00A1;
    private const nint HtCaption = 2;

    private readonly Window _window;

    private Point _ultimoClique;
    private long _ultimoCliqueTicks;

    public WpfWindowChrome(Window window)
    {
        _window = window;
        _window.StateChanged += (_, _) => StateChanged?.Invoke();
    }

    public bool IsMaximized => _window.WindowState is WindowState.Maximized;

    public event Action? StateChanged;

    public void BeginDrag() => _window.Dispatcher.Invoke(() =>
    {
        // O duplo clique na barra de título maximiza — comportamento que o
        // Windows daria de graça se a legenda fosse dele. Como a entregamos ao
        // WebView2, ele não chega: a segunda batida vira outro mouse-down, e o
        // sistema não sintetiza o NCLBUTTONDBLCLK. Detectamos aqui, com os
        // mesmos critérios do sistema (tempo e distância configurados pelo
        // usuário), em vez de confiar no evento dblclick do DOM, que se perde
        // assim que o arrasto nativo rouba o ponteiro.
        if (EhDuploClique())
        {
            ToggleMaximize();
            return;
        }

        var hwnd = new WindowInteropHelper(_window).Handle;

        if (hwnd == nint.Zero)
            return;

        ReleaseCapture();
        SendMessage(hwnd, WmNcLButtonDown, HtCaption, nint.Zero);
    });

    public void Minimize() =>
        _window.Dispatcher.Invoke(() => _window.WindowState = WindowState.Minimized);

    public void ToggleMaximize() => _window.Dispatcher.Invoke(() =>
        _window.WindowState = _window.WindowState is WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized);

    public void Close() => _window.Dispatcher.Invoke(_window.Close);

    private bool EhDuploClique()
    {
        if (!GetCursorPos(out var ponto))
            return false;

        var agora = Environment.TickCount64;
        var atual = new Point(ponto.X, ponto.Y);

        // A tolerância de distância é a do sistema (SM_CXDOUBLECLK/CYDOUBLECLK):
        // ninguém acerta o mesmo pixel duas vezes, e o valor é ajustável nas
        // opções de acessibilidade.
        var dentroDoTempo = agora - _ultimoCliqueTicks <= GetDoubleClickTime();

        var dentroDaArea =
            Math.Abs(atual.X - _ultimoClique.X) <= GetSystemMetrics(SmCxDoubleClk) / 2.0 &&
            Math.Abs(atual.Y - _ultimoClique.Y) <= GetSystemMetrics(SmCyDoubleClk) / 2.0;

        _ultimoClique = atual;

        // Zera o relógio ao reconhecer o par: sem isso, um terceiro clique
        // dentro da janela contaria como novo duplo e a janela ficaria
        // alternando entre maximizada e restaurada.
        _ultimoCliqueTicks = dentroDoTempo && dentroDaArea ? 0 : agora;

        return dentroDoTempo && dentroDaArea;
    }

    // ---------- P/Invoke ----------

    private const int SmCxDoubleClk = 36;
    private const int SmCyDoubleClk = 37;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ReleaseCapture();

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    private static partial nint SendMessage(nint hWnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    private static partial uint GetDoubleClickTime();

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int index);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out PontoNativo ponto);

    [StructLayout(LayoutKind.Sequential)]
    private struct PontoNativo
    {
        public int X;
        public int Y;
    }
}
