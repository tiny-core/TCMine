using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.UI.Components;
using TCMine.Launcher.UI.State;

namespace TCMine.Launcher.UI.Pages;

public partial class ModpacksPage : ComponentBase, IDisposable
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

    [Inject] private LoadCatalog Catalog { get; set; } = default!;

    [Inject] private InstallModpackVersion Installer { get; set; } = default!;

    [Inject] private ListInstances Instances { get; set; } = default!;

    [Inject] private LauncherShellState Shell { get; set; } = default!;

    [Inject] private InstallOperationState Operation { get; set; } = default!;

    /// <summary>Jogo aberto ou instalação em curso: as ações daqui ficam desligadas.</summary>
    [Inject]
    private ActionLock Lock { get; set; } = default!;

    [Inject] private IDialogService Dialogs { get; set; } = default!;

    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    public void Dispose()
    {
        Operation.Changed -= OnOperationChanged;
        Lock.Changed -= OnOperationChanged;
        GC.SuppressFinalize(this);
    }

    protected override async Task OnInitializedAsync()
    {
        // Assina ANTES de carregar: uma instalação que termina entre o assign e
        // o load não é perdida, e reabrir a tela no meio de uma em curso mostra
        // a barra de novo — é para isto que o estado saiu do campo local.
        Operation.Changed += OnOperationChanged;
        Lock.Changed += OnOperationChanged;

        await LoadAsync();
        await RefreshInstalledAsync();
    }

    private void OnOperationChanged() => InvokeAsync(StateHasChanged);

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
        // Light: esta tela nunca mostra tamanho de instância, só se ela existe.
        // A variante cheia soma o disco de cada mundo instalado à toa.
        var manifestos = await Instances.HandleLightAsync(CancellationToken.None);

        _installed = [.. manifestos.Select(m => m.ModpackId)];
        _installedVersions = [.. manifestos.Select(m => m.ModpackVersionId)];
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
        if (Lock.IsLocked)
            return;

        var parameters = new DialogParameters<VersionPickerDialog>
        {
            { d => d.ModpackId, modpack.Id },
            { d => d.ModpackName, modpack.Name },
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
        if (Shell.Pairing?.Config is not { } config || Lock.IsLocked)
            return;

        Operation.Begin(modpack.Id);

        var progress = new Progress<InstallProgress>(Operation.Report);

        try
        {
            // Alvo nulo: instalar pelo catálogo cria sempre uma instância nova.
            // A escolha entre atualizar e duplicar mora na tela de instâncias,
            // que é quem tem uma instância existente em mãos.
            var result = versionId is { } chosen
                ? await Installer.HandleAsync(
                    config.ServerUrl, modpack, chosen, null,
                    progress, CancellationToken.None)
                : await Installer.InstallLatestAsync(
                    config.ServerUrl, modpack, null, ReleaseChannel.Release,
                    progress, CancellationToken.None);

            if (result.Succeeded)
            {
                Snackbar.Add($"{modpack.Name} instalado.", Severity.Success);
                await RefreshInstalledAsync();
            }
            else
                Snackbar.Add(result.Error!, Severity.Error);
        }
        finally
        {
            Operation.Finish();
        }
    }
}
