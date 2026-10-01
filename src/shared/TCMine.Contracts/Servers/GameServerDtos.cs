namespace TCMine.Contracts.Servers;

public sealed record GameServerDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }

    public required Guid ModpackId { get; init; }

    /// <summary>
    ///     A versão é pinada NO SERVIDOR, não no modpack. É o que permite atualizar
    ///     um servidor de cada vez e manter um de testes na versão nova sem
    ///     arrastar todos.
    /// </summary>
    public required Guid ModpackVersionId { get; init; }

    /// <summary>
    ///     Endereço para o servers.dat. Pode incluir porta.
    ///     Nulo quando <see cref="AccessState" /> não é <see cref="ServerAccessState.Granted" />
    ///     — um servidor com whitelist aparece na lista para qualquer jogador
    ///     poder pedir acesso, mas o endereço só sai depois que o pedido é
    ///     aprovado. "Pedir acesso" perderia o sentido se o endereço já
    ///     estivesse ali, pronto para tentar entrar de qualquer forma.
    /// </summary>
    public string? ConnectAddress { get; init; }

    public required GameServerStatus Status { get; init; }
    public int OnlinePlayers { get; init; }
    public int MaxPlayers { get; init; }

    /// <summary>
    ///     Papel do usuário atual NESTE servidor. Governa o que a UI mostra.
    ///     Atenção: esconder botão não é segurança. O Hub e a API precisam checar
    ///     a permissão de novo, sempre. Isto aqui é só para a interface não
    ///     oferecer o que vai dar erro.
    /// </summary>
    public required ServerRoleDto Role { get; init; }

    /// <summary>Se o jogador já pode entrar, já pediu, ou ainda nem pediu.</summary>
    public required ServerAccessState AccessState { get; init; }
}

/// <summary>
///     Onde o jogador está, em relação a um servidor com whitelist.
///     Um servidor sem whitelist é sempre <see cref="Granted" /> para quem está
///     autenticado — não há o que pedir.
/// </summary>
public enum ServerAccessState
{
    /// <summary>Tem Membership: vê o endereço, pode entrar.</summary>
    Granted,

    /// <summary>Já pediu; o dono ainda não decidiu.</summary>
    Pending,

    /// <summary>Nem pediu ainda — é o estado que mostra o botão "Pedir acesso".</summary>
    None
}

public enum GameServerStatus
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Crashed,
    Updating
}

/// <summary>
///     Papel POR RECURSO, não global: alguém pode ser Admin do survival e apenas
///     Member do creative.
///     Os valores são espaçados a cada 10 de propósito, para caber um papel
///     intermediário no futuro sem renumerar os existentes.
/// </summary>
public enum ServerRoleDto
{
    /// <summary>Vê status e contagem de jogadores. Sem console.</summary>
    Member = 0,

    /// <summary>Lê o console e executa comandos de uma allowlist.</summary>
    Moderator = 10,

    /// <summary>Start/stop, config, update, console completo, backups.</summary>
    Admin = 20,

    /// <summary>Tudo, incluindo deletar o servidor e gerenciar membros.</summary>
    Owner = 30
}
