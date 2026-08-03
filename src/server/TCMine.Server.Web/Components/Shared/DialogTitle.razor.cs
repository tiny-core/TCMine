using Microsoft.AspNetCore.Components;

namespace TCMine.Server.Web.Components.Shared;

public partial class DialogTitle : ComponentBase
{
    [Parameter] [EditorRequired] public string Icon { get; set; } = "";
    [Parameter] [EditorRequired] public string Text { get; set; } = "";
    [Parameter] public string? Subtitle { get; set; }
}
