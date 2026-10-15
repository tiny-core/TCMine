using Microsoft.AspNetCore.Components;

namespace TCMine.Server.Web.Components.Features.Network;

/// <summary>A rede de casa (IP local) e a faixa de portas, partes do mesmo <see cref="NetworkForm" />.</summary>
public partial class HomeNetworkEditor : ComponentBase
{
    [Parameter] [EditorRequired] public NetworkForm Form { get; set; } = default!;
}
