using Microsoft.AspNetCore.Components;

namespace TCMine.Server.Web.Components.Shared;

public partial class AuthShell : ComponentBase
{
    [Parameter] [EditorRequired] public string Title { get; set; } = "";
    [Parameter] public string? Subtitle { get; set; }

    /// <summary>Mensagem de falha (vem do endpoint pela query string).</summary>
    [Parameter]
    public string? Error { get; set; }

    /// <summary>Mensagem de sucesso/confirmação.</summary>
    [Parameter]
    public string? Info { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }
}
