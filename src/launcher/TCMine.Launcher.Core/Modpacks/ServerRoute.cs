using TCMine.Contracts.Servers;
using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Core.Modpacks;

/// <summary>
///     Por onde entrar num servidor, decidido na hora de abrir o jogo.
///     Primeiro a rede de casa, depois o endereço público. A ordem não é gosto:
///     de dentro de casa o endereço público costuma falhar (muitos roteadores não
///     fazem "NAT loopback"), e de fora o endereço local simplesmente não
///     responde — então experimentar o local primeiro acerta nos dois casos, e o
///     custo para quem está fora é só o prazo da sonda.
/// </summary>
public static class ServerRoute
{
    /// <returns>O endereço escolhido, ou nulo quando o servidor não informou nenhum.</returns>
    public static async Task<ServerAddress?> ChooseAsync(
        GameServerDto server, IEndpointProbe probe, CancellationToken ct)
    {
        var published = ServerAddress.Parse(server.ConnectAddress);

        if (ServerAddress.Parse(server.LocalAddress) is { } local
            && await probe.IsReachableAsync(local, ct))
            return local;

        // O público sai mesmo sem sonda: ele pode ser um nome com registro SRV
        // (sem porta), que só o jogo sabe resolver. Se ele também não responder,
        // é o jogo quem diz porquê — com a mensagem do Minecraft, que o jogador
        // já conhece.
        return published ?? ServerAddress.Parse(server.LocalAddress);
    }
}
