using Microsoft.AspNetCore.Components;

namespace TCMine_Server.Components.Pages.Admin.Widgets;

public partial class DashboardHeader : ComponentBase
{
    [Parameter] [EditorRequired] public string ServerVersion { get; set; } = null!;
    [Parameter] [EditorRequired] public string EnvName { get; set; } = null!;
    [Parameter] public string? LauncherVersion { get; set; }
}