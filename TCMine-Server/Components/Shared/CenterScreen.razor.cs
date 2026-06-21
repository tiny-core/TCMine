using Microsoft.AspNetCore.Components;

namespace TCMine_Server.Components.Shared;

public partial class CenterScreen : ComponentBase
{
    [Parameter] public RenderFragment? ChildContent { get; set; }
}