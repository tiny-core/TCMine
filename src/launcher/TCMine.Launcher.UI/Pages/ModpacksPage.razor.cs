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

        var parametros = new DialogParameters<VersionPickerDialog>
        {
            { d => d.ModpackId, modpack.Id }
        };

        var dialogo = await Dialogs.ShowAsync<VersionPickerDialog>(
            $"Instalar {modpack.Name}", parametros);

        var resultado = await dialogo.Result;

        if (resultado?.Data is Guid versao && versao != Guid.Empty)
            await InstallAsync(modpack, versao);
    }

    private Task InstallAsync(ModpackDto modpack) => InstallAsync(modpack, null);

    private async Task InstallAsync(ModpackDto modpack, Guid? versionId)
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
            // Alvo nulo: instalar pelo catálogo cria sempre uma instância nova.
            // A escolha entre atualizar e duplicar mora na tela de instâncias,
            // que é quem tem uma instância existente em mãos.
            var resultado = versionId is { } escolhida
                ? await Installer.HandleAsync(
                    config.ServerUrl, modpack, escolhida, target: null,
                    acompanhamento, CancellationToken.None)
                : await Installer.InstallLatestAsync(
                    config.ServerUrl, modpack, target: null, acompanhamento, CancellationToken.None);

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
