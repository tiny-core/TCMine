using Microsoft.AspNetCore.Components;
using TCMine.Contracts;
using TCMine.Contracts.Servers;

namespace TCMine.Server.Web.Components.Pages;

/// <summary>
///     Code-behind do componente. O nome da classe precisa bater exatamente com
///     o do arquivo razor, e o namespace com a pasta — o compilador gera a
///     outra metade desta partial a partir do markup.
/// </summary>
public partial class Home : ComponentBase
{
    private static readonly GameServerStatus[] StatusExemplo =
    [
        GameServerStatus.Running,
        GameServerStatus.Stopped,
        GameServerStatus.Crashed
    ];

    private static int ProtocolVersion => Protocol.Current;
}
