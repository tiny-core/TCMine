using Microsoft.AspNetCore.Components;
using TCMine_Server.Infrastructure.Server;

namespace TCMine_Server.Components.Pages;

public partial class Home : ComponentBase
{
    // Catálogo de conteúdo da landing (modpacks publicados + estado do feed do launcher)
    [Inject] private ContentCatalog Catalog { get; set; } = null!;

    // Modpacks publicados exibidos na grade (vazio enquanto carrega ou se não houver)
    private IReadOnlyList<ModpackWithServers> _modpacks = [];

    private string? LauncherVersion { get; set; }
    private bool LauncherAvailable { get; set; }

    protected override async Task OnInitializedAsync()
    {
        // Modpacks publicados vêm do banco; versão/disponibilidade do launcher, do feed Velopack
        _modpacks = await Catalog.GetModpacksAsync();
        LauncherVersion = Catalog.LauncherVersion;
        LauncherAvailable = Catalog.LauncherAvailable;
    }
}
