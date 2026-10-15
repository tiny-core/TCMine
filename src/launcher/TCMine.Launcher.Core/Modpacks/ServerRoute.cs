using TCMine.Contracts.Servers;
using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Core.Modpacks;

/// <summary>
///     Por onde entrar num servidor, decidido na hora de abrir o jogo.
///     Primeiro a rede de casa, depois o caminho direto, e só então o túnel. A
///     ordem não é gosto: de dentro de casa o endereço público costuma falhar
///     (muitos roteadores não fazem "NAT loopback"), de fora o endereço local
///     simplesmente não responde, e o túnel é o mais lento dos três — fica para
///     quando os outros não respondem.
///     O caminho direto só é sondado quando HÁ túnel: sem reserva, sondar seria
///     pagar o prazo da sonda para chegar à mesma escolha.
/// </summary>
public static class ServerRoute
{
    public static async Task<ServerRouteChoice> ChooseAsync(
        GameServerDto server, IEndpointProbe probe, CancellationToken ct)
    {
        var published = ServerAddress.Parse(server.ConnectAddress);

        if (ServerAddress.Parse(server.LocalAddress) is { } local
            && await probe.IsReachableAsync(local, ct))
            return ServerRouteChoice.Direct(local);

        if (server.TunnelAddress is { Length: > 0 } tunnel)
        {
            // A sonda vai ao ProbeAddress, e não ao publicado: este pode ser um
            // nome com registro SRV, que uma sonda TCP não lê — ela concluiria
            // "não responde" e mandaria todo mundo pelo túnel à toa.
            if (ServerAddress.Parse(server.ProbeAddress) is { } direct)
            {
                return await probe.IsReachableAsync(direct, ct)
                    ? ServerRouteChoice.Direct(published ?? direct)
                    : ServerRouteChoice.Tunnel(tunnel);
            }

            // Sem por onde sondar: o túnel só quando não há outro caminho.
            if (published is null)
                return ServerRouteChoice.Tunnel(tunnel);
        }

        // O público sai mesmo sem sonda: ele pode ser um nome com registro SRV
        // (sem porta), que só o jogo sabe resolver. Se ele também não responder,
        // é o jogo quem diz porquê — com a mensagem do Minecraft, que o jogador
        // já conhece.
        return ServerRouteChoice.Direct(published ?? ServerAddress.Parse(server.LocalAddress));
    }
}

/// <summary>
///     A escolha: um endereço para o jogo, ou o nome do túnel a abrir antes.
///     Os dois nulos = o servidor não informou caminho nenhum.
/// </summary>
public sealed record ServerRouteChoice(ServerAddress? Address, string? TunnelHost)
{
    public static ServerRouteChoice Direct(ServerAddress? address) => new(address, null);

    public static ServerRouteChoice Tunnel(string host) => new(null, host);
}
