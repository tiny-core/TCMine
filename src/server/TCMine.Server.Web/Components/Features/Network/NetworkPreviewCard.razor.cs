using Microsoft.AspNetCore.Components;

namespace TCMine.Server.Web.Components.Features.Network;

/// <summary>"O que os jogadores recebem", calculado do formulário (ver <see cref="NetworkForm.Preview" />).</summary>
public partial class NetworkPreviewCard : ComponentBase
{
    [Parameter] [EditorRequired] public NetworkForm Form { get; set; } = default!;
}
