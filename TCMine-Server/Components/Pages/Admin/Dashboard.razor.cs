using System.Reflection;
using Microsoft.AspNetCore.Components;
using TCMine_Infrastructure.Server;
using TCMine_Server.Services;

namespace TCMine_Server.Components.Pages.Admin;

/// <summary>
/// Página do dashboard admin. Responsabilidade única: carregar o agregado do dashboard uma vez
/// e distribuí-lo aos widgets (ver Admin/Widgets). Métricas ao vivo, gráfico e formatações vivem
/// nos próprios widgets, não aqui.
/// </summary>
public partial class Dashboard : ComponentBase
{
    // Ambiente atual (Development/Production) — distingue dev de produção no cabeçalho
    [Inject] private IWebHostEnvironment Env { get; set; } = null!;

    // Conteúdo do banco (agregado do dashboard) + estado do feed do launcher
    [Inject] private ContentCatalog Catalog { get; set; } = null!;

    [Inject] private BusyService Busy { get; set; } = null!;

    // Agregado carregado no OnInitializedAsync (null enquanto não chega → widgets mostram skeletons)
    private DashboardData? _data;

    private string EnvName => Env.EnvironmentName;

    // Versão do launcher publicada no feed Velopack (null = ainda não publicado)
    private string? LauncherVersion => Catalog.LauncherVersion;

    // Versão do assembly do servidor (ex.: "1.0.0"); cai para "dev" se não houver
    private static string ServerVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "dev";

    protected override async Task OnInitializedAsync()
    {
        await Busy.RunAsync("Carregando dashboard…", async () => { _data = await Catalog.GetDashboardAsync(); });
    }
}