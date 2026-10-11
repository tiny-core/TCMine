using Microsoft.Extensions.Logging;
using TCMine.Launcher.Core.Connectivity;
using TCMine.Launcher.Core.Identity;

namespace TCMine.Launcher.UI.State;

/// <summary>
///     Mantém o launcher ligado ao servidor, do arranque em diante, e leva o
///     estado dessa ligação até a interface.
///     Antes disto ninguém era dono dela: o canal só abria quando uma tela de
///     catálogo era visitada, nada reagia quando caía, e um arranque com o
///     servidor fora do ar deixava o launcher desligado até ser reaberto.
///     São três trabalhos:
///     sessão → canal: assim que há jogador autenticado e servidor conhecido,
///     pede ao canal que se mantenha aberto;
///     canal → interface: cada mudança de estado vai para o
///     <see cref="LauncherShellState" />, de onde o rodapé lê;
///     servidor fora → servidor de volta: tenta o handshake de novo, sem limite,
///     e quando ele responde retoma a sessão sozinho.
///     Não decide COMO o canal religa — isso é do canal (ver
///     <c>SignalRServerConnection</c>). Aqui mora o "quando" e o "e depois".
/// </summary>
public sealed partial class LinkSupervisor : IDisposable
{
    private readonly IServerConnection _connection;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    // Um handshake de cada vez: o laço de recuperação e o "o canal voltou"
    // podem coincidir, e dois a aplicar estado ao mesmo tempo deixariam a
    // interface com a resposta que chegou por último, não a mais recente.
    private readonly SemaphoreSlim _handshakeGate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();

    // Guardado à parte: ler Token de uma fonte já descartada lança.
    private readonly CancellationToken _lifetimeToken;
    private readonly ILogger<LinkSupervisor> _logger;
    private readonly ServerPairing _pairing;
    private readonly LauncherShellState _shell;
    private readonly SignIn _signIn;
    private readonly UpdateLauncher _updater;

    private bool _attached;
    private bool _everUp;
    private Uri? _keeping;
    private LinkStatus _lastLink;
    private int _recovering;

    /// <param name="delay">
    ///     A espera entre tentativas. Só os testes passam outra: com a real, o
    ///     laço de recuperação dormiria de verdade a meio de um teste.
    /// </param>
    public LinkSupervisor(
        IServerConnection connection,
        LauncherShellState shell,
        ServerPairing pairing,
        SignIn signIn,
        UpdateLauncher updater,
        ILogger<LinkSupervisor> logger,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _connection = connection;
        _shell = shell;
        _pairing = pairing;
        _signIn = signIn;
        _updater = updater;
        _logger = logger;
        _delay = delay ?? Task.Delay;
        _lifetimeToken = _lifetime.Token;
    }

    public void Dispose()
    {
        if (_attached)
        {
            _connection.StateChanged -= OnLinkChanged;
            _shell.Changed -= OnShellChanged;
        }

        _lifetime.Cancel();
        _lifetime.Dispose();
        _handshakeGate.Dispose();
    }

    /// <summary>
    ///     Pede à moldura que navegue. Evento, e não um NavigationManager aqui
    ///     dentro: isto roda fora do circuito, onde navegar não é seguro.
    /// </summary>
    public event Action<string>? NavigationRequested;

    /// <summary>
    ///     Começa a acompanhar. Chamado uma vez, pela moldura; repetir não faz
    ///     nada — uma segunda assinatura duplicaria cada aviso.
    /// </summary>
    public void Attach()
    {
        if (_attached)
            return;

        _attached = true;

        _connection.StateChanged += OnLinkChanged;
        _shell.Changed += OnShellChanged;

        // O estado de partida: a sessão pode já existir quando isto é chamado.
        OnLinkChanged();
        OnShellChanged();
    }

    /// <summary>
    ///     Refaz o handshake e põe o launcher em dia com o que o servidor
    ///     respondeu. Verdadeiro quando o servidor está no ar e fala a nossa
    ///     língua.
    ///     É o que o arranque faz, com duas diferenças que vêm de já haver uma
    ///     sessão de uso a decorrer: a atualização do launcher vira aviso em vez
    ///     de reinício, e uma sessão que já existe não é pedida de novo.
    /// </summary>
    public async Task<bool> SyncWithServerAsync(CancellationToken ct)
    {
        await _handshakeGate.WaitAsync(ct);

        try
        {
            var pairing = await _pairing.ResumeAsync(ct);

            // O canal está de pé e o handshake diz que o servidor não responde:
            // quem tem razão é o canal, que é a medida mais recente. Aplicar
            // este resultado desligaria os menus por causa de um pedido perdido.
            if (pairing.Status is PairingStatus.Unreachable && _connection.Status is LinkStatus.Up)
                return false;

            _shell.Apply(pairing);

            if (!pairing.IsOnline)
                return false;

            _shell.SetPendingUpdate(await _updater.FindAsync(pairing.Server, ct));

            if (_shell.IsSignedIn)
                return true;

            _shell.Apply(await _signIn.ResumeAsync(pairing.Config!, ct));

            // Sem credencial guardada (ou recusada): só o jogador resolve.
            if (!_shell.IsSignedIn)
                NavigationRequested?.Invoke("/login");

            return true;
        }
        finally
        {
            _handshakeGate.Release();
        }
    }

    private void OnLinkChanged()
    {
        var status = _connection.Status;
        var previous = _lastLink;

        _lastLink = status;
        _shell.Apply(status);

        if (status is not LinkStatus.Up || previous is LinkStatus.Up)
            return;

        // O canal VOLTOU (a primeira ligação não conta: o arranque acabou de
        // fazer o handshake). A queda mais comum é o servidor a reiniciar por
        // atualização — justamente quando o protocolo, o nome e a versão do
        // launcher que ele oferece podem ter mudado.
        if (_everUp)
            _ = RefreshAfterReturnAsync();

        _everUp = true;
    }

    private void OnShellChanged()
    {
        KeepLinkWhileSignedIn();
        RecoverWhileOffline();
    }

    private void KeepLinkWhileSignedIn()
    {
        var wanted = _shell.IsSignedIn ? _shell.Pairing?.Config?.ServerUrl : null;

        if (wanted is null)
        {
            // Sem sessão não se pede nada. Quem fecha o canal é o SignOut — aqui
            // só se esquece o endereço, para o próximo login voltar a pedir.
            _keeping = null;
            _everUp = false;

            return;
        }

        if (wanted.Equals(_keeping))
            return;

        // Gravado ANTES de pedir: o pedido muda o estado do canal, que volta
        // aqui pelo Changed — sem isto, pediria de novo a si mesmo.
        _keeping = wanted;
        _connection.KeepConnected(wanted);
    }

    private void RecoverWhileOffline()
    {
        if (!_shell.IsOffline || _lifetime.IsCancellationRequested)
            return;

        // Um laço só. O SetRecovering abaixo dispara o Changed que nos trouxe
        // aqui, e sem esta guarda cada aviso abriria mais um.
        if (Interlocked.Exchange(ref _recovering, 1) is 1)
            return;

        _shell.SetRecovering(true);
        _ = RecoverAsync(_lifetimeToken);
    }

    /// <summary>
    ///     Tenta o handshake até o servidor responder. Sem limite, com a mesma
    ///     espera do canal — é o mesmo problema, um degrau antes.
    /// </summary>
    private async Task RecoverAsync(CancellationToken ct)
    {
        try
        {
            for (var failures = 1; !ct.IsCancellationRequested; failures++)
            {
                await _delay(ReconnectBackoff.DelayFor(failures), ct);

                // Alguém chegou antes durante a espera (o canal voltou e refez
                // o handshake): não há mais o que recuperar.
                if (!_shell.IsOffline)
                    return;

                try
                {
                    if (await SyncWithServerAsync(ct))
                        return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Uma tentativa que rebenta não pode matar o laço: o jogador
                    // ficaria desligado até reabrir, que é o que isto veio acabar.
                    LogRecoveryFailed(ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // O launcher está a fechar.
        }
        finally
        {
            Interlocked.Exchange(ref _recovering, 0);
            _shell.SetRecovering(false);
        }
    }

    private async Task RefreshAfterReturnAsync()
    {
        try
        {
            await SyncWithServerAsync(_lifetimeToken);
        }
        catch (OperationCanceledException)
        {
            // O launcher está a fechar.
        }
        catch (Exception ex)
        {
            LogRefreshFailed(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tentativa de voltar a ligar ao servidor falhou.")]
    private partial void LogRecoveryFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Não foi possível refazer o handshake depois de o canal voltar.")]
    private partial void LogRefreshFailed(Exception ex);
}
