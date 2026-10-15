using Microsoft.AspNetCore.Components;
using TCMine.Contracts.Servers;
using TCMine.Launcher.Core.Modpacks;

namespace TCMine.Launcher.UI.Components;

/// <summary>
///     Os servidores que rodam o pack ativo.
///     Recebe a lista pronta em vez de a ir buscar: quem sabe qual é a instância
///     ativa é a tela, e um componente que consultasse o catálogo sozinho faria
///     uma segunda ida à rede por informação que já está em memória.
///     Pedir acesso é exceção: é uma ação de UM clique numa linha, e vive no
///     <see cref="ServerAccessButton" />, o mesmo da aba Servidores.
/// </summary>
public partial class HomeServers : ComponentBase
{
    [Parameter] [EditorRequired] public IReadOnlyList<GameServerDto> Servers { get; set; } = [];

    /// <summary>
    ///     Abrir o resgate de convite é decisão de quem usa o componente — ele
    ///     só mostra a lista que recebeu, não sabe diálogo nem catálogo.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public EventCallback OnRedeemInvite { get; set; }

    /// <summary>
    ///     Abrir o jogo já dentro do servidor. É da tela: é ela que mostra o
    ///     andamento — que inclui instalar a instância do servidor, se ainda
    ///     não houver.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public EventCallback<GameServerDto> OnJoin { get; set; }

    /// <summary>Por que nenhum servidor pode ser aberto agora (jogo aberto, sem pareamento), ou nulo.</summary>
    [Parameter]
    public string? JoinBlockedReason { get; set; }

    /// <summary>O servidor que está a ser aberto, para o spinner ficar na linha certa.</summary>
    [Parameter]
    public Guid? Joining { get; set; }

    private string? WhyCannotJoin(GameServerDto server) =>
        JoinBlockedReason ?? ServerEntry.Unavailable(server);
}
