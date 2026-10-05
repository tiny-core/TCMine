using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Servers;
using TCMine.Launcher.Core.Connectivity;
using TCMine.Launcher.Core.Modpacks;

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

    /// <summary>
    ///     Abrir o jogo já dentro do servidor. É da tela: é ela que sabe a
    ///     instância ativa e mostra o andamento da abertura.
    /// </summary>
    [Parameter] [EditorRequired] public EventCallback<GameServerDto> OnJoin { get; set; }

    /// <summary>Por que nenhum servidor pode ser aberto agora (jogo aberto, sem pareamento), ou nulo.</summary>
    [Parameter] public string? JoinBlockedReason { get; set; }

    /// <summary>O servidor que está a ser aberto, para o spinner ficar na linha certa.</summary>
    [Parameter] public Guid? Joining { get; set; }

    /// <summary>
    ///     Versão instalada da instância ativa, para decidir "Entrar" ou
    ///     "Atualizar e entrar" sem esperar o clique — e para recusar de
    ///     antemão o servidor que está para trás (JoinServer recusaria do
    ///     mesmo jeito, mas só depois do clique).
    /// </summary>
    [Parameter] public string? ActiveVersion { get; set; }

    [Inject] private IServerConnection Connection { get; set; } = default!;

    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    /// <summary>
    ///     Negativo se o servidor está atrás, positivo se está na frente, nulo
    ///     se não dá para comparar (versão apagada, ou alguma das duas não é
    ///     SemVer).
    /// </summary>
    private int? OrderVersusActive(GameServerDto server) =>
        server.ModpackVersionLabel is { } label && ActiveVersion is { } active
            ? ModpackVersionOrder.Compare(label, active)
            : null;

    private bool NeedsUpdate(GameServerDto server) => OrderVersusActive(server) > 0;

    private string? WhyCannotJoin(GameServerDto server)
    {
        if (JoinBlockedReason is not null)
            return JoinBlockedReason;

        if (server.Status is not GameServerStatus.Running)
            return "O servidor não está no ar agora.";

        // Descer de versão por cima de uma instância partiria o mundo (mods
        // que somem levam os blocos e itens que registraram) — a mesma regra
        // do JoinServer, só que aqui dita o botão ANTES do clique.
        if (OrderVersusActive(server) < 0)
            return "Este servidor está numa versão anterior à da sua instância instalada.";

        return null;
    }

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
