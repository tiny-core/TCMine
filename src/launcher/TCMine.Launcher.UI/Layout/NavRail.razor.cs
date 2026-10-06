using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;
using TCMine.Launcher.UI.State;

namespace TCMine.Launcher.UI.Layout;

public partial class NavRail : ComponentBase, IDisposable
{
    /// <summary>
    ///     O que cada entrada precisa. Jogar e Instâncias leem do disco e
    ///     continuam de pé sem servidor — é disso que o launcher vive quando a
    ///     rede não está. Catálogo e novidades vêm do servidor e não têm como
    ///     fingir.
    /// </summary>
    private static readonly NavItem[] Primary =
    [
        new("Jogar", Icons.Material.Filled.PlayArrow, "/"),
        new("Instâncias", Icons.Material.Filled.Layers, "/instances"),
        new("Modpacks", Icons.Material.Filled.Widgets, "/modpacks", NeedsServer: true),
        new("Novidades", Icons.Material.Filled.Campaign, "/news", NeedsServer: true)
    ];

    private static readonly NavItem[] Secondary =
    [
        new("Definições", Icons.Material.Filled.Settings, "/settings"),
        new("Sobre", Icons.Material.Filled.Info, "/about")
    ];

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Inject] private LauncherShellState Shell { get; set; } = default!;

    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
        Shell.Changed -= OnShellChanged;
        GC.SuppressFinalize(this);
    }

    protected override void OnInitialized()
    {
        Navigation.LocationChanged += OnLocationChanged;

        // O trilho tem de reagir ao servidor cair ou voltar: sem isto, os itens
        // ficariam desligados até o jogador navegar para outro sítio — que é
        // justamente o que eles impedem.
        Shell.Changed += OnShellChanged;
    }

    private void OnShellChanged() => InvokeAsync(StateHasChanged);

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => InvokeAsync(StateHasChanged);

    /// <summary>
    ///     Desligado só quando SABEMOS que o servidor não está: durante o
    ///     arranque a resposta ainda não chegou, e piscar os itens desligados
    ///     para depois os ligar é pior do que esperar um segundo.
    /// </summary>
    private bool IsDisabled(NavItem item) => item.NeedsServer && Shell.IsOffline;

    private string TooltipFor(NavItem item) =>
        IsDisabled(item) ? $"{item.Label} — precisa do servidor, que não está a responder" : item.Label;

    private bool IsActive(NavItem item) =>
        NavMatch.IsActive(item.Href, Navigation.ToBaseRelativePath(Navigation.Uri));

    private sealed record NavItem(string Label, string Icon, string Href, bool NeedsServer = false);
}
