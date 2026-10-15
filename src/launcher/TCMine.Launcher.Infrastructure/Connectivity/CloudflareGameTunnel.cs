using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using Microsoft.Extensions.Logging;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Modpacks;

namespace TCMine.Launcher.Infrastructure.Connectivity;

/// <summary>
///     O túnel do lado do jogador, falando o que o <c>cloudflared access tcp</c>
///     fala: um WebSocket seguro até o nome do túnel (wss://nome), e os bytes do
///     jogo em quadros binários, nos dois sentidos. A Cloudflare entrega cada
///     conexão ao cloudflared da máquina do servidor, que a liga à porta do jogo.
///     Uma conexão WebSocket por conexão TCP do jogo: o Minecraft abre mais de
///     uma (a consulta da lista de servidores e a do jogo), e cada uma vira um
///     fluxo independente, como seria direto.
///     Sem token do Cloudflare Access: a entrada não pode estar atrás de uma
///     política do Access (é o que a tela de configurações diz ao admin).
/// </summary>
public sealed partial class CloudflareGameTunnel(ILogger<CloudflareGameTunnel> logger) : IGameTunnel
{
    /// <summary>A conexão de teste: o bastante para um TLS e um handshake, curto para não segurar o "Jogar".</summary>
    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

    private readonly ILogger<CloudflareGameTunnel> _logger = logger;

    public async Task<IGameTunnelSession?> OpenAsync(string hostname, CancellationToken ct)
    {
        Uri endpoint;
        try
        {
            endpoint = new Uri($"wss://{hostname.Trim()}");
        }
        catch (UriFormatException ex)
        {
            LogFalhou(ex, hostname);
            return null;
        }

        // Antes de abrir a porta local: um túnel que não atende faria o jogo
        // bater numa porta que aceita e corta, com um erro que não diz nada.
        try
        {
            using var probe = await ConnectAsync(endpoint, ct);
            await probe.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, ct);
        }
        catch (Exception ex) when (ex is WebSocketException or HttpRequestException or OperationCanceledException
                                       && !ct.IsCancellationRequested)
        {
            LogFalhou(ex, hostname);
            return null;
        }

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var session = new Session(listener, endpoint, this);
        session.Start();

        LogAberto(hostname, session.LocalAddress.Port ?? 0);
        return session;
    }

    internal static async Task<ClientWebSocket> ConnectAsync(Uri endpoint, CancellationToken ct)
    {
        var socket = new ClientWebSocket();

        // Ping de tempos em tempos: um jogador parado num menu não manda nada,
        // e a Cloudflare corta WebSocket ocioso.
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        socket.Options.SetRequestHeader("User-Agent", "TCMine-Launcher");

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(ConnectTimeout);

        try
        {
            await socket.ConnectAsync(endpoint, limit.Token);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Túnel até {Host} aberto na porta local {Port}.")]
    private partial void LogAberto(string host, int port);

    [LoggerMessage(Level = LogLevel.Warning, Message = "O túnel até {Host} não respondeu.")]
    private partial void LogFalhou(Exception ex, string host);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Uma conexão pelo túnel terminou com erro.")]
    private partial void LogConexaoCaiu(Exception ex);

    /// <summary>A porta local e as conexões que passam por ela.</summary>
    private sealed class Session(TcpListener listener, Uri endpoint, CloudflareGameTunnel owner) : IGameTunnelSession
    {
        private readonly CancellationTokenSource _stop = new();
        private Task _accepting = Task.CompletedTask;

        // Calculada, e não guardada num inicializador: o parâmetro do construtor
        // primário já é usado pelos métodos, e usá-lo nos dois lugares é aviso.
        public ServerAddress LocalAddress =>
            new(IPAddress.Loopback.ToString(), ((IPEndPoint)listener.LocalEndpoint).Port);

        public void Start() => _accepting = AcceptLoopAsync(_stop.Token);

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync();
            listener.Stop();

            try
            {
                await _accepting;
            }
            catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException)
            {
                // Parar de aceitar é justamente o que se pediu.
            }

            _stop.Dispose();
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(ct);

                // Cada conexão do jogo segue sozinha; uma que cai não derruba as outras.
                _ = BridgeAsync(client, ct);
            }
        }

        private async Task BridgeAsync(TcpClient client, CancellationToken ct)
        {
            using (client)
            {
                client.NoDelay = true; // o jogo manda pacotes pequenos; esperar para juntá-los é latência
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);

                try
                {
                    using var socket = await ConnectAsync(endpoint, linked.Token);
                    var stream = client.GetStream();

                    // Quando um sentido acaba (o jogo fechou, ou o servidor),
                    // o outro não tem mais para quem falar: cancela os dois.
                    var up = PumpUpAsync(stream, socket, linked.Token);
                    var down = PumpDownAsync(socket, stream, linked.Token);

                    await Task.WhenAny(up, down);
                    await linked.CancelAsync();

                    try
                    {
                        await Task.WhenAll(up, down);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }
                catch (Exception ex) when (ex is WebSocketException or IOException or SocketException
                                               or HttpRequestException or OperationCanceledException)
                {
                    if (!ct.IsCancellationRequested)
                        owner.LogConexaoCaiu(ex);
                }
            }
        }

        /// <summary>Do jogo para o túnel.</summary>
        private static async Task PumpUpAsync(NetworkStream from, ClientWebSocket to, CancellationToken ct)
        {
            var buffer = new byte[16 * 1024];

            while (true)
            {
                var read = await from.ReadAsync(buffer, ct);
                if (read == 0)
                {
                    // O jogo fechou a conexão: avisa o outro lado do jeito certo.
                    if (to.State is WebSocketState.Open)
                        await to.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, ct);
                    return;
                }

                await to.SendAsync(buffer.AsMemory(0, read), WebSocketMessageType.Binary, true, ct);
            }
        }

        /// <summary>Do túnel para o jogo.</summary>
        private static async Task PumpDownAsync(ClientWebSocket from, NetworkStream to, CancellationToken ct)
        {
            var buffer = new byte[16 * 1024];

            while (true)
            {
                var received = await from.ReceiveAsync(buffer.AsMemory(), ct);
                if (received.MessageType is WebSocketMessageType.Close)
                    return;

                await to.WriteAsync(buffer.AsMemory(0, received.Count), ct);
            }
        }
    }
}
