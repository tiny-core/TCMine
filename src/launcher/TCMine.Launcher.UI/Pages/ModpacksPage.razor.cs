using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.UI.State;

namespace TCMine.Launcher.UI.Pages;

public partial class ModpacksPage : ComponentBase
{
    private CatalogView? _catalog;

    /// <summary>Modpacks com alguma versão instalada nesta máquina.</summary>
    private HashSet<Guid> _installed = [];

    private bool _loading;

    /// <summary>Instalação em curso. Uma de cada vez, de propósito — ver Install.</summary>
    private Guid? _installing;

    private InstallProgress? _progress;

    [Inject] private LoadCatalog Catalog { get; set; } = default!;

    [Inject] private InstallModpackVersion Installer { get; set; } = default!;

    [Inject] private ListInstances Instances { get; set; } = default!;

    [Inject] private LauncherShellState Shell { get; set; } = default!;

    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
        await RefreshInstalledAsync();
    }

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

    private async Task RefreshInstalledAsync()
    {
        var instaladas = await Instances.HandleAsync(CancellationToken.None);

        _installed = [.. instaladas.Select(i => i.Manifest.ModpackId)];
    }

    /// <summary>
    ///     Instala, e uma por vez.
    ///     Duas instalações simultâneas disputariam o mesmo content store e a
    ///     mesma banda, e o jogador veria duas barras andando pela metade da
    ///     velocidade — mais lento no total e pior de acompanhar.
    /// </summary>
    private async Task InstallAsync(ModpackDto modpack)
    {
        if (Shell.Pairing?.Config is not { } config || _installing is not null)
            return;

        _installing = modpack.Id;
        _progress = InstallProgress.Planning;

        var acompanhamento = new Progress<InstallProgress>(p =>
        {
            _progress = p;
            InvokeAsync(StateHasChanged);
        });

        try
        {
            var resultado = await Installer.InstallLatestAsync(
                config.ServerUrl, modpack, acompanhamento, CancellationToken.None);

            if (resultado.Succeeded)
            {
                Snackbar.Add($"{modpack.Name} instalado.", Severity.Success);
                await RefreshInstalledAsync();
            }
            else
            {
                Snackbar.Add(resultado.Error!, Severity.Error);
            }
        }
        finally
        {
            _installing = null;
            _progress = null;
        }
    }
}
