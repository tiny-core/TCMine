using Microsoft.AspNetCore.Components;

namespace TCMine.Server.Web.Components.Shared;

public partial class ManagementCard : ComponentBase
{
    [Parameter] [EditorRequired] public string Icon { get; set; } = "";
    [Parameter] [EditorRequired] public string Title { get; set; } = "";
    [Parameter] [EditorRequired] public string Description { get; set; } = "";
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public EventCallback OnManage { get; set; }
}
