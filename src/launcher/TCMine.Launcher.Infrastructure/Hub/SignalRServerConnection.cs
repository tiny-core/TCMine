using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
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

    private LauncherHubClient? _client;

    public bool IsConnected => _client?.State is HubConnectionState.Connected;

    public event Action? StateChanged;

    public async Task ConnectAsync(Uri serverUrl, CancellationToken ct)
    {
        await _porta.WaitAsync(ct);

        try
        {
            await FecharAsync();

            var cliente = factory.Create(serverUrl);

            await cliente.ConnectAsync(ct);

            _client = cliente;

            LogConectado(serverUrl);
        }
        finally
        {
            _porta.Release();
        }

        StateChanged?.Invoke();
    }

    public async Task DisconnectAsync()
    {
        await _porta.WaitAsync();

        try
        {
            await FecharAsync();
        }
        finally
        {
            _porta.Release();
        }

        StateChanged?.Invoke();
    }

    public Task<IReadOnlyList<ModpackDto>> GetModpacksAsync(CancellationToken ct) =>
        Exigir().GetModpacksAsync();

    public Task<IReadOnlyList<GameServerDto>> GetServersAsync(CancellationToken ct) =>
        Exigir().GetServersAsync();

    public Task<ModpackVersionDto?> GetLatestVersionAsync(Guid modpackId, CancellationToken ct) =>
        Exigir().GetLatestVersionAsync(modpackId);

    public Task<ModpackVersionDto> GetModpackVersionAsync(Guid versionId, CancellationToken ct) =>
        Exigir().GetModpackVersionAsync(versionId);

    public async ValueTask DisposeAsync() => await FecharAsync();

    /// <summary>
    ///     Chamar sem conexão é erro de programação, não estado esperado: as
    ///     telas só consultam depois de a moldura ter ligado. Falhar aqui com
    ///     mensagem clara é melhor que devolver lista vazia, que a tela exibiria
    ///     como "o servidor não tem modpacks".
    /// </summary>
    private LauncherHubClient Exigir() =>
        _client ?? throw new InvalidOperationException("O canal com o servidor não está aberto.");

    private async Task FecharAsync()
    {
        if (_client is null)
            return;

        var anterior = _client;
        _client = null;

        try
        {
            await anterior.DisposeAsync();
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao fechar o canal anterior.")]
    private partial void LogFalhaAoFechar(Exception ex);
}
