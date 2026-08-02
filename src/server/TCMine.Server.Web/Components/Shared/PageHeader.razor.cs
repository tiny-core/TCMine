using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace TCMine.Server.Web.Components.Shared;

public partial class PageHeader : ComponentBase
{
    [Parameter] [EditorRequired] public string Title { get; set; } = "";

    [Parameter] public string? Subtitle { get; set; }

    /// <summary>Ícone opcional exibido à esquerda do título.</summary>
    [Parameter]
    public string? Icon { get; set; }

    /// <summary>Trilha de navegação opcional, exibida acima do título.</summary>
    [Parameter]
    public List<BreadcrumbItem>? Breadcrumbs { get; set; }

    /// <summary>Ações da página (botões), alinhadas à direita.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
