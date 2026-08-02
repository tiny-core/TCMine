using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace TCMine.Server.Web.Components.Shared;

public partial class StatCard : ComponentBase
{
    [Parameter] [EditorRequired] public string Icon { get; set; } = "";
    [Parameter] [EditorRequired] public string Label { get; set; } = "";
    [Parameter] [EditorRequired] public string Value { get; set; } = "";

    /// <summary>Cor do ícone/avatar. Nome evita colidir com o enum Color no markup.</summary>
    [Parameter]
    public Color IconColor { get; set; } = Color.Primary;
}
