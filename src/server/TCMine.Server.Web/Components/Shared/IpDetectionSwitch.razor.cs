using Microsoft.AspNetCore.Components;

namespace TCMine.Server.Web.Components.Shared;

/// <summary>
///     O interruptor da detecção do IP público, com a explicação. Componente
///     porque aparece em dois modos da aba Rede, e as duas cópias divergiriam.
///     O valor é o gravado: "desligada" (o padrão é ligada).
/// </summary>
public partial class IpDetectionSwitch : ComponentBase
{
    [Parameter] public bool Disabled { get; set; }

    [Parameter] public EventCallback<bool> DisabledChanged { get; set; }

    private Task OnChanged(bool enabled) => DisabledChanged.InvokeAsync(!enabled);
}
