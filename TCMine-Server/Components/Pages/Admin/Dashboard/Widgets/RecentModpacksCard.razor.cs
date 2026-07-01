using Microsoft.AspNetCore.Components;
using TCMine_Server.Infrastructure.Server;

namespace TCMine_Server.Components.Pages.Admin.Dashboard.Widgets;

public partial class RecentModpacksCard : ComponentBase
{
    [Parameter] public IReadOnlyList<RecentModpack>? Modpacks { get; set; }
}