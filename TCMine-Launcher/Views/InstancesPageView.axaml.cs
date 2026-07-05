using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using TCMine_Domain.Launcher;
using TCMine_Launcher.ViewModels;

namespace TCMine_Launcher.Views;

public partial class InstancesPageView : UserControl
{
    public InstancesPageView()
    {
        InitializeComponent();
    }

    private MainWindowViewModel? Shell => (DataContext as InstancesPageViewModel)?.Shell;

    private static InstalledModpack? InstanceOf(object? sender)
    {
        return (sender as Control)?.DataContext as InstalledModpack;
    }

    private void OnDelete(object? sender, RoutedEventArgs e)
    {
        if (InstanceOf(sender) is { } i) Shell?.DeleteInstance(i);
    }

    private void OnOpenShaders(object? sender, RoutedEventArgs e)
    {
        if (InstanceOf(sender) is { } i) Shell?.OpenInstanceSubfolder(i, "shaderpacks");
    }

    private void OnOpenTextures(object? sender, RoutedEventArgs e)
    {
        if (InstanceOf(sender) is { } i) Shell?.OpenInstanceSubfolder(i, "resourcepacks");
    }

    private void OnEditRam(object? sender, RoutedEventArgs e)
    {
        if (InstanceOf(sender) is { } i && Shell is { } shell && TopLevel.GetTopLevel(this) is Window owner)
            new MemoryWindow { DataContext = new MemoryEditViewModel(shell, i) }.Show(owner);
    }

    private async void OnExport(object? sender, RoutedEventArgs e)
    {
        if (InstanceOf(sender) is not { } i || Shell is not { } shell || TopLevel.GetTopLevel(this) is not { } top)
            return;

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Exportar instância",
            SuggestedFileName = i.Name + ".tcmine",
            DefaultExtension = "zip",
            FileTypeChoices = [new FilePickerFileType("Zip") { Patterns = ["*.zip"] }]
        });

        if (file?.TryGetLocalPath() is { } path) await shell.ExportInstanceAsync(i, path);
    }

    private async void OnImport(object? sender, RoutedEventArgs e)
    {
        if (Shell is not { } shell || TopLevel.GetTopLevel(this) is not { } top) return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Importar instância",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Zip") { Patterns = ["*.zip"] }]
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path) await shell.ImportInstanceAsync(path);
    }
}