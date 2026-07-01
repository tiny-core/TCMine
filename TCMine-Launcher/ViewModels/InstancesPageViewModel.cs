namespace TCMine_Launcher.ViewModels;

/// <summary>
/// Aba "Instâncias": lista das instaladas. As ações (RAM, pastas, exportar, importar, eliminar) são
/// tratadas pela View (code-behind) por envolverem janelas/file-pickers, delegando à shell.
/// </summary>
public sealed class InstancesPageViewModel(MainWindowViewModel shell) : ViewModelBase
{
    public MainWindowViewModel Shell { get; } = shell;
}
