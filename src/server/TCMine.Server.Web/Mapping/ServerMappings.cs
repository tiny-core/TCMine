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
    public static GameServerDto ToDto(this AccessibleServer accessible)
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

            // Ainda não há contador de jogadores: nada no servidor pergunta ao
            // jogo quantos estão online, e o caminho previsto para isso é o push
            // ServerPlayerCountChanged, que também não tem quem o dispare. Zero
            // é a verdade com o servidor parado e um valor defasado com ele no
            // ar — a alternativa seria um docker exec por servidor a cada
            // listagem, que custa mais do que o dado vale aqui.
            OnlinePlayers = 0,
            MaxPlayers = server.MaxPlayers,
            Role = accessible.Role
        };
    }
}
