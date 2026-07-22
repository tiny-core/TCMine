using TCMine.Contracts.Servers;
using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Servers;

public sealed class GameServer : Entity, IOwnedEntity
{
    public required string Name { get; set; }
    public required Guid ModpackId { get; set; }

    /// <summary>
    ///     A versão é pinada aqui, no servidor, e não no modpack.
    ///     Sem isso você não consegue atualizar um servidor de cada vez, nem
    ///     manter um de testes na versão nova enquanto o principal fica estável.
    /// </summary>
    public required Guid ModpackVersionId { get; set; }

    /// <summary>Endereço publicado no servers.dat do cliente.</summary>
    public required string ConnectAddress { get; set; }

    public GameServerStatus Status { get; set; } = GameServerStatus.Stopped;

    /// <summary>ID do container itzg/minecraft-server. Nulo se nunca foi criado.</summary>
    public string? ContainerId { get; set; }

    public int MemoryMb { get; set; } = 4096;
    public int MaxPlayers { get; set; } = 20;

    /// <summary>
    ///     Senha do RCON. NUNCA sai do servidor: não vai em DTO, não vai em log,
    ///     não aparece na UI. O launcher pede um comando pelo Hub e o servidor é
    ///     quem traduz para RCON — quem tem a senha tem controle total da máquina
    ///     do jogo.
    /// </summary>
    public required string RconSecret { get; set; }

    public Guid OwnerId { get; set; }
}