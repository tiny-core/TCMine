using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.UI.Components;
using TCMine.Launcher.UI.State;

namespace TCMine.Launcher.UI.Pages;

public partial class ModpacksPage : ComponentBase
{
    private CatalogView? _catalog;

    /// <summary>Modpacks com alguma versão instalada nesta máquina.</summary>
    private HashSet<Guid> _installed = [];

    /// <summary>
    ///     As versões que já existem no disco, de qualquer pack.
    ///     O seletor usa-as para desligar o que já está instalado: instalar a
    ///     mesma versão outra vez criaria uma segunda instância idêntica à
    ///     primeira — disco gasto para ter duas cópias do mesmo, e duas entradas
    ///     indistinguíveis na lista de instâncias.
    /// </summary>
    private HashSet<Guid> _installedVersions = [];

    private bool _loading;

    /// <summary>Instalação em curso. Uma de cada vez, de propósito — ver Install.</summary>
    private Guid? _installing;

    private InstallProgress? _progress;

    [Inject] private LoadCatalog Catalog { get; set; } = default!;

    [Inject] private InstallModpackVersion Installer { get; set; } = default!;

    [Inject] private ListInstances Instances { get; set; } = default!;

    [Inject] private LauncherShellState Shell { get; set; } = default!;

    [Inject] private IDialogService Dialogs { get; set; } = default!;

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
        _installedVersions = [.. instaladas.Select(i => i.Manifest.ModpackVersionId)];
    }

    /// <summary>
    ///     Instala, e uma por vez.
    ///     Duas instalações simultâneas disputariam o mesmo content store e a
    ///     mesma banda, e o jogador veria duas barras andando pela metade da
    ///     velocidade — mais lento no total e pior de acompanhar.
    /// </summary>
    /// <summary>
    ///     Pergunta a versão e instala essa.
    ///     Sempre numa instância NOVA, como o botão ao lado: escolher uma versão
    ///     antiga é quase sempre para a ter ao lado da atual — se fosse para
    ///     substituir, o caminho é atualizar pela tela de instâncias, que é onde
    ///     a pergunta sobre o mundo é feita.
    /// </summary>
    private async Task InstallSpecificAsync(ModpackDto modpack)
    {
        if (_installing is not null)
            return;

        var parameters = new DialogParameters<VersionPickerDialog>
        {
            { d => d.ModpackId, modpack.Id },
            { d => d.InstalledVersionIds, _installedVersions }
        };

        var dialog = await Dialogs.ShowAsync<VersionPickerDialog>(
            $"Instalar {modpack.Name}", parameters);

        var result = await dialog.Result;

        if (result?.Data is Guid version && version != Guid.Empty)
            await InstallAsync(modpack, version);
    }

    private Task InstallAsync(ModpackDto modpack) => InstallAsync(modpack, null);

    private async Task InstallAsync(ModpackDto modpack, Guid? versionId)
    {
        if (Shell.Pairing?.Config is not { } config || _installing is not null)
            return;

        _installing = modpack.Id;
        _progress = InstallProgress.Planning;

        var progress = new Progress<InstallProgress>(p =>
        {
            _progress = p;
            InvokeAsync(StateHasChanged);
        });

        try
        {
            // Alvo nulo: instalar pelo catálogo cria sempre uma instância nova.
            // A escolha entre atualizar e duplicar mora na tela de instâncias,
            // que é quem tem uma instância existente em mãos.
            var result = versionId is { } chosen
                ? await Installer.HandleAsync(
                    config.ServerUrl, modpack, chosen, target: null,
                    progress, CancellationToken.None)
                : await Installer.InstallLatestAsync(
                    config.ServerUrl, modpack, target: null, ReleaseChannel.Release,
                    progress, CancellationToken.None);

            if (result.Succeeded)
            {
                Snackbar.Add($"{modpack.Name} instalado.", Severity.Success);
                await RefreshInstalledAsync();
            }
            else
            {
                Snackbar.Add(result.Error!, Severity.Error);
            }
        }
        finally
        {
            _installing = null;
            _progress = null;
        }
    }
}
