using Microsoft.AspNetCore.Components;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.UI.State;

namespace TCMine.Launcher.UI.Pages;

public partial class ModpacksPage : ComponentBase
{
    private CatalogView? _catalog;
    private bool _loading;

    [Inject] private LoadCatalog Catalog { get; set; } = default!;

    [Inject] private LauncherShellState Shell { get; set; } = default!;

    protected override Task OnInitializedAsync() => LoadAsync();

    /// <summary>
    ///     Quantos jogadores, quando o servidor está no ar. Parado, o número
    ///     seria sempre zero e pareceria um servidor vazio em vez de desligado.
    /// </summary>
    private static string ServerLabel(CatalogEntry entrada) =>
        entrada.IsAnyServerRunning
            ? $"{entrada.OnlinePlayers} online"
            : "Servidor parado";

    private async Task LoadAsync()
    {
        if (Shell.Pairing?.Config is not { } config)
            return;

        _loading = true;

        try
        {
            _catalog = await Catalog.HandleAsync(config.ServerUrl, CancellationToken.None);
        }
        finally
        {
            _loading = false;
        }
    }
}
