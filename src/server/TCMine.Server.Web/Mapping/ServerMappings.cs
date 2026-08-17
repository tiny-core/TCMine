using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;

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
    public static GameServerDto ToDto(this AccessibleServer accessible, IPlayerCountSource players)
    {
        var server = accessible.Server;

        return new GameServerDto
        {
            Id = server.Id,
            Name = server.Name,
            ModpackId = server.ModpackId,
            ModpackVersionId = server.ModpackVersionId,
            ConnectAddress = server.ConnectAddress,
            Status = server.Status,

            // Última contagem amostrada. Zero quando ainda não se sabe — o
            // campo é int no contrato, então não há como dizer "não sei", e
            // zero é o que menos engana num servidor recém-ligado. A partir daí
            // o push ServerPlayerCountChanged mantém o número vivo sem o
            // launcher precisar perguntar.
            OnlinePlayers = players.TryGet(server.Id) ?? 0,
            MaxPlayers = server.MaxPlayers,
            Role = accessible.Role
        };
    }
}
