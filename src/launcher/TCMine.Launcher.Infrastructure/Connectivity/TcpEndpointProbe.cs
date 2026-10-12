using System.Net.Sockets;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Modpacks;

namespace TCMine.Launcher.Infrastructure.Connectivity;

/// <summary>
///     Abre uma conexão TCP e fecha logo em seguida, só para saber se o outro
///     lado atende. Não fala o protocolo do Minecraft: para escolher entre a
///     rede de casa e a internet, "atende nessa porta" basta, e o jogo é quem
///     faz a conversa de verdade logo depois.
/// </summary>
public sealed class TcpEndpointProbe : IEndpointProbe
{
    /// <summary>
    ///     Quanto esperar. Na rede de casa a resposta vem em milissegundos; o
    ///     prazo é o que quem está FORA paga a cada "Entrar", porque o endereço
    ///     local dele não vai a lugar nenhum. Curto o bastante para não parecer
    ///     travado, longo o bastante para um Wi-Fi lento.
    /// </summary>
    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromMilliseconds(1500);

    private const int MinecraftDefaultPort = 25565;

    public async Task<bool> IsReachableAsync(ServerAddress address, CancellationToken ct)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(ProbeTimeout);

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(address.Host, address.Port ?? MinecraftDefaultPort, limit.Token);
            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return false; // o prazo esgotou: para a escolha, é o mesmo que recusar
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
