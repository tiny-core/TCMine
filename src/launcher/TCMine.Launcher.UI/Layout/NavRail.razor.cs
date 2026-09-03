using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;

namespace TCMine.Launcher.UI.Layout;

public partial class NavRail : ComponentBase, IDisposable
{
    private static readonly NavItem[] Primary =
    [
        new("Jogar", Icons.Material.Filled.PlayArrow, "/"),
        new("Instâncias", Icons.Material.Filled.Layers, "/instances"),
        new("Modpacks", Icons.Material.Filled.Widgets, "/modpacks"),
        new("Novidades", Icons.Material.Filled.Campaign, "/news")
    ];

    private static readonly NavItem[] Secondary =
    [
        new("Definições", Icons.Material.Filled.Settings, "/settings")
    ];

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
        GC.SuppressFinalize(this);
    }

    protected override void OnInitialized() => Navigation.LocationChanged += OnLocationChanged;

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => InvokeAsync(StateHasChanged);

    private bool IsActive(NavItem item) =>
        NavMatch.IsActive(item.Href, Navigation.ToBaseRelativePath(Navigation.Uri));

    private sealed record NavItem(string Label, string Icon, string Href);
}
