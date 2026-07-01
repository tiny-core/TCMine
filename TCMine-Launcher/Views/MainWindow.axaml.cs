using Avalonia.Controls;
using Avalonia.Interactivity;
using TCMine_Launcher.ViewModels;

namespace TCMine_Launcher.Views;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private MainWindowViewModel? Vm => DataContext as MainWindowViewModel;

    /// <summary>Abre a janela do registo de eventos (botão "Eventos" no footer).</summary>
    private void OnOpenLog(object? sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        new LogWindow { DataContext = Vm }.Show(this);
    }

    /// <summary>Abre a janela de memória do modpack ativo (botão de RAM no footer).</summary>
    private void OnOpenMemory(object? sender, RoutedEventArgs e)
    {
        if (Vm?.Active is { } active)
            new MemoryWindow { DataContext = new MemoryEditViewModel(Vm, active) }.Show(this);
    }
}
