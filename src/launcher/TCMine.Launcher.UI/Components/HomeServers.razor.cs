using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Servers;
using TCMine.Launcher.Core.Connectivity;

namespace TCMine.Launcher.UI.Components;

/// <summary>
///     Os servidores que rodam o pack ativo.
///     Recebe a lista pronta em vez de a ir buscar: quem sabe qual é a instância
///     ativa é a tela, e um componente que consultasse o catálogo sozinho faria
///     uma segunda ida à rede por informação que já está em memória.
///     Pedir acesso é exceção: é uma ação de UM clique numa linha, sem diálogo
///     nem estado que a tela precise guardar — fica contida aqui, como o
///     resgate de convite (<see cref="RedeemInviteDialog" />) já injeta o
///     próprio caso de uso em vez de bubbling tudo para cima.
/// </summary>
public partial class HomeServers : ComponentBase
{
    private readonly HashSet<Guid> _pedindo = [];
    private readonly HashSet<Guid> _pedidosFeitos = [];

    [Parameter] [EditorRequired] public IReadOnlyList<GameServerDto> Servers { get; set; } = [];

    /// <summary>
    ///     Abrir o resgate de convite é decisão de quem usa o componente — ele
    ///     só mostra a lista que recebeu, não sabe diálogo nem catálogo.
    /// </summary>
    [Parameter] [EditorRequired] public EventCallback OnRedeemInvite { get; set; }

    [Inject] private IServerConnection Connection { get; set; } = default!;

    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    /// <summary>
    ///     Pendente de verdade (o servidor já respondeu "Pending") OU acabou de
    ///     ser pedido nesta sessão — sem isto, o botão voltaria a aparecer
    ///     "Pedir acesso" entre o clique e o catálogo recarregar da próxima vez,
    ///     parecendo que o pedido não foi enviado.
    /// </summary>
    private bool EstaPendente(GameServerDto server) =>
        server.AccessState is ServerAccessState.Pending || _pedidosFeitos.Contains(server.Id);

    private async Task PedirAcessoAsync(GameServerDto server)
    {
        if (_pedindo.Contains(server.Id))
            return;

        _pedindo.Add(server.Id);

        try
        {
            await Connection.RequestServerAccessAsync(server.Id, CancellationToken.None);
            _pedidosFeitos.Add(server.Id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Snackbar.Add($"Não foi possível pedir acesso: {ex.Message}", Severity.Error);
        }
        finally
        {
            _pedindo.Remove(server.Id);
        }
    }
}
