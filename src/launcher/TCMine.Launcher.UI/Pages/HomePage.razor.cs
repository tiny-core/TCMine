using Microsoft.AspNetCore.Components;
using TCMine.Contracts.Servers;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.UI.State;

namespace TCMine.Launcher.UI.Pages;

public partial class HomePage : ComponentBase
{
    private InstalledInstance? _active;
    private bool _needsChoice;
    private bool _loading = true;
    private IReadOnlyList<GameServerDto> _servers = [];

    [Inject] private ChooseInstance Active { get; set; } = default!;

    [Inject] private LoadCatalog Catalog { get; set; } = default!;

    [Inject] private LauncherShellState Shell { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var vista = await Active.CurrentAsync(CancellationToken.None);

            _active = vista.Active;
            _needsChoice = vista.NeedsChoice;
        }
        finally
        {
            _loading = false;
        }

        // Depois de a tela já poder desenhar: o card da instância é o que
        // importa e vem do disco, enquanto os servidores dependem de rede. Sem
        // esta ordem, um servidor lento deixaria a tela de jogar em branco.
        await CarregarServidoresAsync();
    }

    /// <summary>
    ///     Os servidores do pack ativo, quando há ligação.
    ///     Falhar aqui é silencioso de propósito: a tela de jogar tem de servir
    ///     offline, e um erro vermelho sobre o catálogo faria parecer que a
    ///     instância instalada também está com problema — quando ela está
    ///     inteira no disco.
    /// </summary>
    private async Task CarregarServidoresAsync()
    {
        if (_active is null || Shell.Pairing?.Config is not { } config)
            return;

        var catalogo = await Catalog.HandleAsync(config.ServerUrl, CancellationToken.None);

        if (catalogo.Failed)
            return;

        _servers = catalogo.Entries
            .FirstOrDefault(e => e.Modpack.Id == _active.Manifest.ModpackId)
            ?.Servers ?? [];

        StateHasChanged();
    }
}
