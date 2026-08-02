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

    /// <summary>
    ///     Quando o mundo deste servidor foi inicializado (primeiro boot que gerou
    ///     o level.dat). Null = nunca ligou, ainda nao tem mundo.
    ///     E o seam do backup: trocar a versao de um servidor COM mundo exige
    ///     snapshot antes (mods removidos/rebaixados podem corromper o save). Sem
    ///     mundo, a troca e o re-apontar simples e imediato. Nada preenche isto na
    ///     fatia 1 — so a orquestracao (fatia 3) o fara ao subir o container.
    /// </summary>
    public DateTimeOffset? WorldInitializedAt { get; set; }

    /// <summary>Já tem mundo gravado? Deriva de WorldInitializedAt.</summary>
    public bool HasWorld => WorldInitializedAt is not null;

    public Guid OwnerId { get; set; }
}
