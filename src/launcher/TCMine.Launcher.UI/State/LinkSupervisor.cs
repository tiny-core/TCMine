using TCMine.Launcher.Core.Connectivity;

namespace TCMine.Launcher.UI.State;

/// <summary>
///     Mantém o canal com o servidor aberto enquanto houver sessão, e leva o
///     estado dele até a interface.
///     Antes disto ninguém era dono do canal: ele só abria quando uma tela de
///     catálogo era visitada (<c>LoadCatalog</c>), e nada reagia quando caía.
///     São duas ligações, uma em cada sentido:
///     sessão → canal: assim que há jogador autenticado e servidor conhecido,
///     pede ao canal que se mantenha aberto;
///     canal → interface: cada mudança de estado vai para o
///     <see cref="LauncherShellState" />, de onde o rodapé lê.
///     Não decide nada sobre COMO religar — isso é do canal (ver
///     <c>SignalRServerConnection</c>). Aqui mora só o "quando".
/// </summary>
public sealed class LinkSupervisor(IServerConnection connection, LauncherShellState shell) : IDisposable
{
    private bool _attached;
    private Uri? _keeping;

    public void Dispose()
    {
        if (!_attached)
            return;

        connection.StateChanged -= OnLinkChanged;
        shell.Changed -= OnShellChanged;
    }

    /// <summary>
    ///     Começa a acompanhar. Chamado uma vez, pela moldura; repetir não faz
    ///     nada — uma segunda assinatura duplicaria cada aviso.
    /// </summary>
    public void Attach()
    {
        if (_attached)
            return;

        _attached = true;

        connection.StateChanged += OnLinkChanged;
        shell.Changed += OnShellChanged;

        // O estado de partida: a sessão pode já existir quando isto é chamado.
        OnLinkChanged();
        OnShellChanged();
    }

    private void OnLinkChanged() => shell.Apply(connection.Status);

    private void OnShellChanged()
    {
        var wanted = shell.IsSignedIn ? shell.Pairing?.Config?.ServerUrl : null;

        if (wanted is null)
        {
            // Sem sessão não se pede nada. Quem fecha o canal é o SignOut — aqui
            // só se esquece o endereço, para o próximo login voltar a pedir.
            _keeping = null;

            return;
        }

        if (wanted.Equals(_keeping))
            return;

        // Gravado ANTES de pedir: o pedido muda o estado do canal, que volta
        // aqui pelo Changed — sem isto, pediria de novo a si mesmo.
        _keeping = wanted;
        connection.KeepConnected(wanted);
    }
}
