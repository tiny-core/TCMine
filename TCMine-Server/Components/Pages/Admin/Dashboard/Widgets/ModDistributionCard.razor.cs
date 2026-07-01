using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine_Server.Infrastructure.Server;

namespace TCMine_Server.Components.Pages.Admin.Dashboard.Widgets;

public partial class ModDistributionCard : ComponentBase
{
    // Cores das fatias na ordem client/server/shared (Info/Success/Warning do tema)
    private readonly ChartOptions _donutOptions = new()
    {
        ChartPalette = ["var(--mud-palette-info)", "var(--mud-palette-success)", "var(--mud-palette-warning)"]
    };

    private readonly string[] _donutLabels = ["Cliente", "Servidor", "Ambos"];

    [Parameter] public DashboardData? Data { get; set; }

    // Total de vínculos por-modpack (denominador coerente das três fatias)
    private int TotalLinks => Data is null ? 0 : Data.ClientMods + Data.ServerMods + Data.SharedMods;

    // Série única cujos pontos de dado viram as fatias do donut (ordem = rótulos/paleta).
    // No MudBlazor 9 o donut usa ChartSeries + ChartLabels (InputData/InputLabels foram removidos).
    private List<ChartSeries<double>> _donutSeries => Data is null
        ? []
        : [new ChartSeries<double> { Name = "Mods", Data = new double[] { Data.ClientMods, Data.ServerMods, Data.SharedMods } }];
}
