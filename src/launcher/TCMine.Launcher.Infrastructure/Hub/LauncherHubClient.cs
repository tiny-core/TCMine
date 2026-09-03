using System.Net;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TCMine.Contracts.Hubs;
using TCMine.Contracts.Modpacks;
using TCMine.Contracts.Servers;

namespace TCMine.Launcher.Infrastructure.Hub;

/// <summary>
///     Wrapper tipado sobre a HubConnection.
///     Escrito à mão porque o source generator de proxy do SignalR nunca saiu
///     de preview. A vantagem sobre chamar InvokeAsync com string mágica: o
///     compilador cobra quando a assinatura de IServerHub muda, em vez de o erro
///     só aparecer em runtime na forma de método não encontrado.
///     Implementa IServerHub para o lado do envio e expõe eventos .NET para o
///     lado do recebimento — a UI se inscreve neles sem saber que existe SignalR.
/// </summary>
public sealed partial class LauncherHubClient : IServerHub, IAsyncDisposable
{
    private readonly HubConnection _connection;
    private readonly ILogger<LauncherHubClient> _logger;

    public LauncherHubClient(
        Uri serverUrl,
        CookieContainer cookies,
        ILogger<LauncherHubClient> logger)
    {
        _logger = logger;

        _connection = new HubConnectionBuilder()
            .WithUrl(new Uri(serverUrl, HubRoutes.Main), options =>
            {
                // O MESMO pote de cookies dos pedidos HTTP, e não um token.
                // A sessão emitida no login é um cookie — o mesmo do painel — e
                // o hub a lê como qualquer requisição autenticada. Um segundo
                // caminho de credencial aqui seria mais uma coisa para expirar
                // sozinha e mais uma para manter em dia.
                //
                // O container é compartilhado por referência de propósito: quando
                // o cookie é renovado, a reconexão pega o novo sem recriar nada.
                options.Cookies = cookies;
            })
            .AddMessagePackProtocol()
            // Reconecta sozinho com backoff. Sem argumento, a política vai
            // até cerca de 30s de intervalo e desiste; passar um array
            // customizado permite tentar por mais tempo, já que servidor
            // reiniciando por update pode demorar.
            .WithAutomaticReconnect([
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            ])
            .Build();

        RegisterHandlers();

        _connection.Reconnected += async _ =>
        {
            LogReconnected();

            if (Reconnected is not null)
                await Reconnected.Invoke();
        };

        _connection.Closed += error =>
        {
            if (error is not null)
                LogConnectionClosed(error);

            return Task.CompletedTask;
        };
    }

    public HubConnectionState State => _connection.State;

    public ValueTask DisposeAsync() => _connection.DisposeAsync();

    // ---------- Envio: IServerHub ----------
    // Cada método traduz uma chamada tipada para o InvokeAsync correspondente.
    // O nameof garante que o nome enviado é o do método da interface.

    public Task<IReadOnlyList<ModpackDto>> GetModpacksAsync() =>
        _connection.InvokeAsync<IReadOnlyList<ModpackDto>>(nameof(IServerHub.GetModpacksAsync));

    public Task<ModpackVersionDto> GetModpackVersionAsync(Guid versionId) =>
        _connection.InvokeAsync<ModpackVersionDto>(nameof(IServerHub.GetModpackVersionAsync), versionId);

    public Task<ModpackVersionDto?> GetLatestVersionAsync(Guid modpackId) =>
        _connection.InvokeAsync<ModpackVersionDto?>(nameof(IServerHub.GetLatestVersionAsync), modpackId);

    public Task<IReadOnlyList<GameServerDto>> GetServersAsync() =>
        _connection.InvokeAsync<IReadOnlyList<GameServerDto>>(nameof(IServerHub.GetServersAsync));

    public Task SubscribeServerAsync(Guid serverId) =>
        _connection.InvokeAsync(nameof(IServerHub.SubscribeServerAsync), serverId);

    public Task UnsubscribeServerAsync(Guid serverId) =>
        _connection.InvokeAsync(nameof(IServerHub.UnsubscribeServerAsync), serverId);

    public Task<CommandResultDto> SendCommandAsync(Guid serverId, string command, IReadOnlyList<string> args)
    {
        return _connection.InvokeAsync<CommandResultDto>(
            nameof(IServerHub.SendCommandAsync), serverId, command, args);
    }

    // Eventos do servidor viram eventos .NET. A UI assina; o transporte
    // fica escondido.
    public event Action<Guid, Guid>? ModpackVersionPublished;
    public event Action<Guid, GameServerStatus>? ServerStatusChanged;
    public event Action<Guid, int, int>? PlayerCountChanged;
    public event Action<Guid, ConsoleLineDto>? ConsoleLineReceived;
    public event Action<Guid, ServerRoleDto>? RoleChanged;

    /// <summary>Disparado ao reconectar. É o gatilho da reconciliação.</summary>
    public event Func<Task>? Reconnected;

    public Task ConnectAsync(CancellationToken ct) => _connection.StartAsync(ct);

    // ---------- Recebimento: ILauncherClient ----------

    private void RegisterHandlers()
    {
        // Os nomes precisam bater exatamente com os métodos de
        // ILauncherClient. O nameof amarra ao contrato: renomear no
        // Contracts quebra a compilação aqui, em vez de silenciosamente
        // parar de receber o evento.
        _connection.On<Guid, Guid>(nameof(ILauncherClient.ModpackVersionPublished),
            (modpackId, versionId) => ModpackVersionPublished?.Invoke(modpackId, versionId));

        _connection.On<Guid, GameServerStatus>(nameof(ILauncherClient.ServerStatusChanged),
            (serverId, status) => ServerStatusChanged?.Invoke(serverId, status));

        _connection.On<Guid, int, int>(nameof(ILauncherClient.ServerPlayerCountChanged),
            (serverId, online, max) => PlayerCountChanged?.Invoke(serverId, online, max));

        _connection.On<Guid, ConsoleLineDto>(nameof(ILauncherClient.ConsoleLine),
            (serverId, line) => ConsoleLineReceived?.Invoke(serverId, line));

        _connection.On<Guid, ServerRoleDto>(nameof(ILauncherClient.RoleChanged),
            (serverId, role) => RoleChanged?.Invoke(serverId, role));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Reconectado ao servidor.")]
    private partial void LogReconnected();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Conexão com o servidor encerrada.")]
    private partial void LogConnectionClosed(Exception ex);
}
