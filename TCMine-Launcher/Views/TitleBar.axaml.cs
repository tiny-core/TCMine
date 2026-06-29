using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TCMine_Launcher.Views;

/// <summary>
/// Barra de título para janelas sem chrome nativo (<c>SystemDecorations="None"</c>): logótipo + título
/// + minimizar/fechar, e arrasto da janela. Resolve a janela-pai sozinha (via <see cref="TopLevel"/>).
/// </summary>
public partial class TitleBar : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<TitleBar, string>(nameof(Title), "TCMine Launcher");

    public static readonly StyledProperty<bool> ShowMinimizeProperty =
        AvaloniaProperty.Register<TitleBar, bool>(nameof(ShowMinimize), true);

    public TitleBar() => InitializeComponent();

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool ShowMinimize
    {
        get => GetValue(ShowMinimizeProperty);
        set => SetValue(ShowMinimizeProperty, value);
    }

    private Window? Host => TopLevel.GetTopLevel(this) as Window;

    private void OnDrag(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            Host?.BeginMoveDrag(e);
    }

    private void OnMinimize(object? sender, RoutedEventArgs e)
    {
        if (Host is { } window) window.WindowState = WindowState.Minimized;
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Host?.Close();
}
