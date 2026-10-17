using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using TCMine.Contracts.Hubs;
using TCMine.Contracts.Identity;
using TCMine.Contracts.Modpacks;
using TCMine.Contracts.Servers;
using TCMine.Launcher.Core.Connectivity;

namespace TCMine.Launcher.Infrastructure.Hub;

/// <summary>
///     A porta <see cref="IServerConnection" /> por cima do hub.
///     Guarda UMA conexão para a aplicação inteira. Abrir uma por tela custaria
///     um handshake e uma reconexão independente para cada uma, e o servidor
///     veria o mesmo jogador como vários — o que estragaria contagem de
///     presença e assinatura de grupo mais adiante.
///     Ligar duas vezes é operação normal, não erro: acontece ao entrar depois
///     de sair, e ao trocar de servidor. A conexão anterior é encerrada antes.
///     São duas redes de segurança, uma dentro da outra. Uma queda comum é
///     reaberta pelo próprio SignalR, sem limite de tentativas; o que ele não
///     cobre — a primeira ligação falhar, ou o servidor fechar o canal de vez —
///     é coberto pelo vigia de <see cref="KeepConnected" />.
/// </summary>
public sealed partial class SignalRServerConnection(
    LauncherHubClientFactory factory,
    ILogger<SignalRServerConnection> logger) : IServerConnection
{
    private readonly ILogger<SignalRServerConnection> _logger = logger;

    // Protege a troca de conexão: ligar e desligar podem chegar de threads
    // diferentes (arranque, clique em sair, reconexão) e trocar o campo sem
    // guarda deixaria uma conexão órfã, viva e invisível.
    private readonly SemaphoreSlim _porta = new(1, 1);

    // Guarda só a troca do vigia. À parte da _porta porque KeepConnected é
    // síncrono e não pode esperar por uma ligação em curso.
    private readonly Lock _watcherGate = new();

    // Acorda o vigia quando o canal fica em baixo. Um semáforo e não um evento:
    // um aviso que chegue enquanto o vigia está ocupado fica guardado, em vez de
    // se perder.
    private readonly SemaphoreSlim _wake = new(0);

    private LauncherHubClient? _client;
    private volatile bool _sessionRejected;
    private volatile LinkStatus _status = LinkStatus.Down;
    private CancellationTokenSource? _watcher;
    private Uri? _watched;

    public bool IsConnected => _client?.State is HubConnectionState.Connected;

    public LinkStatus Status => _status;

    public bool SessionRejected => _sessionRejected;

    public event Action? StateChanged;

    public event Action<ResourceKind, Guid?>? Invalidated;

    public async Task ConnectAsync(Uri serverUrl, CancellationToken ct)
    {
        await _porta.WaitAsync(ct);

        try
        {
            await FecharAsync();

            _sessionRejected = false;
            Set(LinkStatus.Connecting);

            var cliente = factory.Create(serverUrl);
            Watch(cliente);

            try
            {
                await cliente.ConnectAsync(ct);
            }
            catch (Exception ex)
            {
                // O cliente que não ligou não pode ficar vivo: ele segura um
                // HttpClient e, pior, os eventos que acabamos de assinar.
                await DiscardAsync(cliente);

                // Antes do Set: quem ouve a mudança de estado já tem de
                // encontrar o motivo.
                _sessionRejected = HubErrors.IsUnauthorized(ex);
                Set(LinkStatus.Down);

                throw;
            }

            _client = cliente;

            Set(LinkStatus.Up);
            LogConectado(serverUrl);
        }
        finally
        {
            _porta.Release();
        }
    }

    public void KeepConnected(Uri serverUrl)
    {
        CancellationToken token;

        lock (_watcherGate)
        {
            if (_watcher is not null && serverUrl.Equals(_watched))
            {
                // Mesmo endereço, mas a sessão tinha sido recusada: quem chama
                // está a dizer que a renovou. O vigia volta a tentar.
                if (_sessionRejected)
                {
                    _sessionRejected = false;
                    _wake.Release();
                }

                return;
            }

            _watcher?.Cancel();
            _watcher?.Dispose();

            _watcher = new CancellationTokenSource();
            _watched = serverUrl;
            token = _watcher.Token;
        }

        // Task.Run: nada do vigia roda na thread de quem chamou (a de interface)
        // nem dentro do lock acima — a primeira ligação já dispara eventos.
        _ = Task.Run(() => WatchLinkAsync(serverUrl, token), CancellationToken.None);
    }

    public async Task DisconnectAsync()
    {
        // Antes de fechar: desligar é uma decisão (sair da conta), e o vigia
        // reabriria o canal no instante seguinte.
        StopWatching();

        await _porta.WaitAsync();

        try
        {
            await FecharAsync();
            Set(LinkStatus.Down);
        }
        finally
        {
            _porta.Release();
        }
    }

    public Task<IReadOnlyList<ModpackDto>> GetModpacksAsync(CancellationToken ct) =>
        Require().GetModpacksAsync();

    public Task<IReadOnlyList<GameServerDto>> GetServersAsync(CancellationToken ct) =>
        Require().GetServersAsync();

    public Task<ModpackVersionDto?> GetLatestVersionAsync(
        Guid modpackId, ReleaseChannel channel, CancellationToken ct) =>
        Require().GetLatestVersionAsync(modpackId, channel);

    public Task<IReadOnlyList<ModpackVersionSummaryDto>> GetVersionsAsync(
        Guid modpackId, ReleaseChannel channel, CancellationToken ct) =>
        Require().GetVersionsAsync(modpackId, channel);

    public Task<IReadOnlyList<ModpackNewsDto>> GetNewsAsync(Guid modpackId, CancellationToken ct) =>
        Require().GetNewsAsync(modpackId);

    public Task<ModpackVersionDto> GetModpackVersionAsync(Guid versionId, CancellationToken ct) =>
        Require().GetModpackVersionAsync(versionId);

    public Task RequestServerAccessAsync(Guid gameServerId, CancellationToken ct) =>
        Require().RequestServerAccessAsync(gameServerId);

    public Task<PlayerModpackSettingsDto?> GetModpackSettingsAsync(Guid modpackId, CancellationToken ct) =>
        Require().GetModpackSettingsAsync(modpackId);

    public Task SaveModpackSettingsAsync(
        Guid modpackId, string keyBindings, string? options, CancellationToken ct) =>
        Require().SaveModpackSettingsAsync(modpackId, keyBindings, options);

    public async ValueTask DisposeAsync()
    {
        StopWatching();
        await FecharAsync();
    }

    /// <summary>
    ///     Chamar sem conexão é erro de programação, não estado esperado: as
    ///     telas só consultam depois de a moldura ter ligado. Falhar aqui com
    ///     mensagem clara é melhor que devolver lista vazia, que a tela exibiria
    ///     como "o servidor não tem modpacks".
    /// </summary>
    private LauncherHubClient Require() =>
        _client ?? throw new InvalidOperationException("O canal com o servidor não está aberto.");

    /// <summary>
    ///     O vigia: enquanto o canal estiver em baixo, tenta abri-lo; com ele de
    ///     pé (ou a ser reaberto pelo SignalR), dorme até ser avisado.
    /// </summary>
    private async Task WatchLinkAsync(Uri serverUrl, CancellationToken ct)
    {
        var failures = 0;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Com a sessão recusada não adianta insistir: fica à espera
                // de alguém a renovar (ver KeepConnected).
                if (_status is not LinkStatus.Down || _sessionRejected)
                {
                    failures = 0;
                    await _wake.WaitAsync(ct);

                    continue;
                }

                try
                {
                    await ConnectAsync(serverUrl, ct);

                    continue;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    if (_sessionRejected)
                    {
                        LogSessionRejected();

                        continue;
                    }

                    failures++;
                    LogRetryScheduled(ex, ReconnectBackoff.DelayFor(failures));
                }

                await Task.Delay(ReconnectBackoff.DelayFor(failures), ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Saiu da conta, trocou de servidor ou fechou o launcher.
        }
    }

    private void StopWatching()
    {
        lock (_watcherGate)
        {
            _watcher?.Cancel();
            _watcher?.Dispose();
            _watcher = null;
            _watched = null;
        }
    }

    /// <summary>
    ///     Traduz os eventos do SignalR em <see cref="LinkStatus" />.
    ///     Cada reação confere se o cliente ainda é o atual: um cliente
    ///     descartado dispara "fechou" ao morrer, e sem a conferência esse aviso
    ///     atrasado derrubaria o estado do canal novo que o substituiu.
    /// </summary>
    private void Watch(LauncherHubClient cliente)
    {
        cliente.Reconnecting += () =>
        {
            if (ReferenceEquals(_client, cliente))
                Set(LinkStatus.Reconnecting);
        };

        cliente.Reconnected += () =>
        {
            if (ReferenceEquals(_client, cliente))
                Set(LinkStatus.Up);

            return Task.CompletedTask;
        };

        cliente.Invalidated += (kind, id) =>
        {
            if (ReferenceEquals(_client, cliente))
                Invalidated?.Invoke(kind, id);
        };

        cliente.Closed += () =>
        {
            if (!ReferenceEquals(_client, cliente))
                return;

            if (cliente.SessionRejected)
            {
                _sessionRejected = true;
                LogSessionRejected();
            }

            Set(LinkStatus.Down);
        };
    }

    private void Set(LinkStatus status)
    {
        if (_status == status)
            return;

        _status = status;

        if (status is LinkStatus.Down)
            _wake.Release();

        StateChanged?.Invoke();
    }

    private async Task FecharAsync()
    {
        if (_client is null)
            return;

        var anterior = _client;
        _client = null;

        await DiscardAsync(anterior);
    }

    private async Task DiscardAsync(LauncherHubClient cliente)
    {
        try
        {
            await cliente.DisposeAsync();
        }
        catch (Exception ex)
        {
            // Fechar não pode falhar para quem chamou: o objetivo era não ter
            // mais conexão, e ela já saiu do campo.
            LogFalhaAoFechar(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Canal aberto com {ServerUrl}.")]
    private partial void LogConectado(Uri serverUrl);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Não foi possível abrir o canal com o servidor; nova tentativa em {Delay}.")]
    private partial void LogRetryScheduled(Exception ex, TimeSpan delay);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "O servidor recusou a sessão no canal; à espera de ela ser renovada.")]
    private partial void LogSessionRejected();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao fechar o canal anterior.")]
    private partial void LogFalhaAoFechar(Exception ex);
}
