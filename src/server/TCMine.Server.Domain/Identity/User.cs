using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Identity;

public sealed class User : Entity
{
    /// <summary>
    ///     Object ID da Microsoft (claim "oid"). É a chave estável de identidade:
    ///     e-mail e nome de exibição mudam, o oid não.
    /// </summary>
    public required string MicrosoftObjectId { get; set; }

    /// <summary>UUID da conta Minecraft, sem hífens. Nulo até o primeiro login no jogo.</summary>
    public string? MinecraftUuid { get; set; }

    public required string DisplayName { get; set; }

    /// <summary>
    ///     Administrador da instalação TCMine inteira — quem hospeda o serviço.
    ///     Não confundir com Admin de um servidor específico.
    /// </summary>
    public bool IsInstanceAdmin { get; set; }

    public DateTimeOffset? LastSeenAt { get; set; }
}

/// <summary>
///     Vínculo entre usuário e servidor, com o papel dele ali.
///     A permissão é sempre relativa a um recurso: não existe "moderador" no
///     vácuo, existe "moderador do servidor X". Um papel global obrigaria a atribuir
///     acesso no console de todos os servidores para quem só modera um.
/// </summary>
public sealed class Membership : Entity
{
    public required Guid UserId { get; set; }
    public required Guid GameServerId { get; set; }
    public required ServerRole Role { get; set; }
}

public enum ServerRole
{
    Member = 0,
    Moderator = 10,
    Admin = 20,
    Owner = 30
}
