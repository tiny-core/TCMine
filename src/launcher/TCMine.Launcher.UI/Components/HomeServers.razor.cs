using Microsoft.AspNetCore.Components;
using TCMine.Contracts.Servers;

namespace TCMine.Launcher.UI.Components;

/// <summary>
///     Os servidores que rodam o pack ativo.
///     Recebe a lista pronta em vez de a ir buscar: quem sabe qual é a instância
///     ativa é a tela, e um componente que consultasse o catálogo sozinho faria
///     uma segunda ida à rede por informação que já está em memória.
/// </summary>
public partial class HomeServers : ComponentBase
{
    [Parameter] [EditorRequired] public IReadOnlyList<GameServerDto> Servers { get; set; } = [];

    /// <summary>
    ///     Abrir o resgate de convite é decisão de quem usa o componente — ele
    ///     só mostra a lista que recebeu, não sabe diálogo nem catálogo.
    /// </summary>
    [Parameter] [EditorRequired] public EventCallback OnRedeemInvite { get; set; }
}
