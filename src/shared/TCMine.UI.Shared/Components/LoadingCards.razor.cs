using Microsoft.AspNetCore.Components;

namespace TCMine.UI.Shared.Components;

public partial class LoadingCards : ComponentBase
{
    /// <summary>Quantos cards fantasma exibir enquanto carrega.</summary>
    [Parameter]
    public int Count { get; set; } = 6;
}
