using Microsoft.AspNetCore.Components;
using TCMine_Infrastructure.Server;

namespace TCMine_Server.Components.Pages.Admin.Widgets;

public partial class DashboardKpis : ComponentBase
{
    [Parameter] public DashboardData? Data { get; set; }
}