using Microsoft.AspNetCore.Components;

namespace TCMine_Server.Components.Pages;

public partial class Home : ComponentBase
{
    // Catálogo de conteúdo da landing (modpacks/servidores + estado do launcher)
    // [Inject] private ContentCatalog Catalog { get; set; } = null!;

    // private IReadOnlyList<ModpackWithServers> Modpacks { get; set; } = [];
    private string? LauncherVersion { get; set; }
    private bool LauncherAvailable { get; set; }

    protected override async Task OnInitializedAsync()
    {
        // Modpacks publicados vêm do banco; versão/disponibilidade do launcher, do feed Velopack
        // Modpacks = await Catalog.GetModpacksAsync();
        // LauncherVersion = Catalog.LauncherVersion;
        // LauncherAvailable = Catalog.LauncherAvailable;
    }
}