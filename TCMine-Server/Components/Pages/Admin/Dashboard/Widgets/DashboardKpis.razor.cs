using Microsoft.AspNetCore.Components;
using TCMine_Server.Infrastructure.Server;

namespace TCMine_Server.Components.Pages.Admin.Dashboard.Widgets;

public partial class DashboardKpis : ComponentBase
{
    [Parameter] public DashboardData? Data { get; set; }
}