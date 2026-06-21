using Microsoft.AspNetCore.Components;
using TCMine_Infrastructure.Server;

namespace TCMine_Server.Components.Pages.Admin.Widgets;

public partial class ModDistributionCard : ComponentBase
{
    [Parameter] public DashboardData? Data { get; set; }

    // Percentuais para as barras; evitam divisão por zero
    private double ClientShare => Data is { Mods: > 0 } ? (double)Data.ClientMods / Data.Mods * 100 : 0;
    private double ServerShare => Data is { Mods: > 0 } ? (double)Data.ServerMods / Data.Mods * 100 : 0;
}