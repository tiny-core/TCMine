using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using TCMine.UI.Shared.Theming;

namespace TCMine.Launcher.App.Chrome;

/// <summary>
///     Pinta a borda que o Windows 11 desenha à volta da janela com a cor das
///     bordas de dentro (<see cref="TcColors.Semantic.Dark.Border" />).
///     A borda é do sistema, não nossa: com a moldura estendida sobre a área de
///     cliente, ela continua lá, na cor de destaque ou cinzenta do Windows, e
///     destoava do traço fino que separa a barra de título, o trilho e o rodapé.
///     A cor vem da mesma constante do tema, e não de um hex repetido aqui, para
///     não divergirem no dia em que a paleta mudar.
///     No Windows 10 o atributo não existe e a chamada só falha — a janela fica
///     como sempre esteve.
/// </summary>
internal static partial class WindowBorder
{
    /// <summary>DWMWA_BORDER_COLOR, do Windows 11 (build 22000) em diante.</summary>
    private const int DwmwaBorderColor = 34;

    /// <summary>
    ///     Aplica quando a janela ganha HWND. Antes disso não há o que pintar, e
    ///     depois do primeiro quadro a borda apareceria com a cor velha por um
    ///     instante.
    /// </summary>
    public static void MatchTheme(Window window) =>
        window.SourceInitialized += (_, _) => Apply(new WindowInteropHelper(window).Handle);

    private static void Apply(nint hwnd)
    {
        if (hwnd == nint.Zero)
            return;

        var color = ToColorRef(TcColors.Semantic.Dark.Border);

        // O resultado é ignorado de propósito: falhar aqui (Windows 10) não é
        // erro, é só a borda de sempre.
        _ = DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref color, sizeof(int));
    }

    /// <summary>"#RRGGBB" para COLORREF, que o Windows lê ao contrário: 0x00BBGGRR.</summary>
    private static int ToColorRef(string hex)
    {
        var rgb = int.Parse(hex.AsSpan(1, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        var r = (rgb >> 16) & 0xFF;
        var g = (rgb >> 8) & 0xFF;
        var b = rgb & 0xFF;

        return (b << 16) | (g << 8) | r;
    }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
}
