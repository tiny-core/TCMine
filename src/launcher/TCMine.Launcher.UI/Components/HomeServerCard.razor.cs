using Microsoft.AspNetCore.Components;
using TCMine.Contracts.Servers;

namespace TCMine.Launcher.UI.Components;

public partial class HomeServerCard : ComponentBase
{
    [Parameter] [EditorRequired] public GameServerDto Server { get; set; } = default!;
}
