using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Identity;

public sealed class User : Entity
{
    /// <summary>
    ///     Object ID da Microsoft (claim "oid"). Nulo para quem só entrou pelo
    ///     launcher: ele manda só o token do Minecraft para o servidor
    ///     (<c>AuthenticateMinecraftUser</c>), não o token Microsoft que teria o
    ///     oid — então um jogador comum nunca tem este campo preenchido, só
    ///     quem já entrou no painel pela Microsoft (<c>AuthenticateMicrosoftUser</c>).
    ///     Não há mais login local: toda conta nasce de uma Microsoft ou de um
    ///     Minecraft, nunca de e-mail e senha.
    /// </summary>
    public string? MicrosoftObjectId { get; set; }

    /// <summary>
    ///     UUID da conta Minecraft, sem hífens. Nulo até a conta vincular um
    ///     Minecraft — obrigatório para quem entra pelo launcher, opcional para
    ///     quem entra pelo painel (a Microsoft sozinha já basta para administrar;
    ///     o Minecraft aí é oportunista, e pode ser vinculado depois).
    /// </summary>
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

/// <summary>
///     Vínculo entre usuário e modpack, com o papel dele ali.
///     Mesma forma do <see cref="Membership" /> de servidor, e pelo mesmo
///     motivo: permissão é sempre relativa a um recurso. Quem cria um modpack
///     vira Owner dele (ver CreateModpack/ImportUpstreamPack); só ele — ou o
///     admin da instalação, que manda em tudo — pode conceder Editor a
///     outra conta.
/// </summary>
public sealed class ModpackMembership : Entity
{
    public required Guid UserId { get; set; }
    public required Guid ModpackId { get; set; }
    public required ModpackRole Role { get; set; }
}

public enum ModpackRole
{
    /// <summary>Edita mods, overrides, cria e publica versões.</summary>
    Editor = 0,

    /// <summary>Editor + gerencia quem mais edita, e pode apagar o modpack.</summary>
    Owner = 10
}
