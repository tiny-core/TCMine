using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Web.Mapping;

/// <summary>
///     Traduz servidores de domínio para o DTO que o launcher recebe.
///     Num lugar só pelo mesmo motivo das outras traduções: o
///     <see cref="Server.Domain.Servers.GameServer" /> carrega o
///     <c>RconSecret</c>, e quem tem essa senha controla a máquina do jogo. A
///     decisão de não incluí-lo tem de morar num arquivo, não na memória de quem
///     escreve o próximo endpoint.
/// </summary>
public static class ServerMappings
{
    /// <param name="publicHost">
    ///     Host público da instalação (<c>GetPublicHost</c>), para os servidores
    ///     de endereço automático. Nulo quando não se conhece nenhum.
    /// </param>
    public static GameServerDto ToDto(
        this AccessibleServer accessible, IPlayerCountSource players, IReadOnlyDictionary<Guid, string> versionLabels,
        string? publicHost)
    {
        var server = accessible.Server;

        return new GameServerDto
        {
            Id = server.Id,
            Name = server.Name,
            ModpackId = server.ModpackId,
            ModpackVersionId = server.ModpackVersionId,
            ModpackVersionLabel = versionLabels.GetValueOrDefault(server.ModpackVersionId),

            // Só sai com o acesso concedido — ver a nota em GameServerDto.
            // E sai RESOLVIDO, nunca o texto cru: o launcher entrega isto ao jogo
            // como está, e um endereço sem ":porta" de um servidor fora da 25565
            // mandava o jogador bater na porta de outro servidor. Sem endereço
            // nenhum (automático, e host público desconhecido) vai nulo, que é
            // como o contrato já diz "não há por onde entrar".
            ConnectAddress = accessible.AccessState is ServerAccessState.Granted
                             && GameAddress.Resolve(server.ConnectAddress, server.GamePort, publicHost)
                                 is { Length: > 0 } address
                ? address
                : null,
            Status = server.Status,

            // Última contagem amostrada. Zero quando ainda não se sabe — o
            // campo é int no contrato, então não há como dizer "não sei", e
            // zero é o que menos engana num servidor recém-ligado. A partir daí
            // o push ServerPlayerCountChanged mantém o número vivo sem o
            // launcher precisar perguntar.
            OnlinePlayers = players.TryGet(server.Id) ?? 0,
            MaxPlayers = server.MaxPlayers,
            Role = accessible.Role,
            AccessState = accessible.AccessState
        };
    }
}
