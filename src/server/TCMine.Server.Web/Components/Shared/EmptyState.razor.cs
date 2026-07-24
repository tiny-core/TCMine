using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace TCMine.Server.Web.Components.Shared;

public partial class EmptyState : ComponentBase
{
    [Parameter] [EditorRequired] public string Icon { get; set; } = Icons.Material.Filled.Inbox;

    [Parameter] [EditorRequired] public string Title { get; set; } = "";

    [Parameter] public string? Description { get; set; }

    /// <summary>Ação opcional — normalmente um botão de "criar".</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}