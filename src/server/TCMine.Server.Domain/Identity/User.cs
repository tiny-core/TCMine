using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Identity;

public sealed class User : Entity
{
    /// <summary>
    ///     E-mail: é por ele que se faz login com conta local, e é o que liga uma
    ///     conta local à conta Microsoft quando a mesma pessoa usa as duas.
    ///     Nulo para quem entrou pelo launcher: o perfil do Minecraft devolve
    ///     UUID e nome de jogador, nunca e-mail. Sintetizar um endereço falso só
    ///     para preencher a coluna criaria uma conta que aparenta ter login
    ///     local e caminho de recuperação de senha — nenhum dos dois existe.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    ///     Hash da senha da conta local. Nulo quando o usuário só entra pela
    ///     Microsoft — nunca guardamos senha em claro, e conta sem senha
    ///     simplesmente não passa pelo login local.
    /// </summary>
    public string? PasswordHash { get; set; }

    /// <summary>
    ///     Object ID da Microsoft (claim "oid"). É a chave estável de identidade
    ///     do lado Microsoft: e-mail e nome de exibição mudam, o oid não. Nulo
    ///     enquanto a conta for só local.
    /// </summary>
    public string? MicrosoftObjectId { get; set; }

    /// <summary>
    ///     Hash SHA-256 do token de recuperação de senha em aberto. Guardamos o
    ///     hash, não o token: se o banco vazar, os links de reset já emitidos não
    ///     servem para nada. Nulo quando não há pedido pendente.
    /// </summary>
    public string? PasswordResetTokenHash { get; set; }

    /// <summary>Quando o token de recuperação expira. Nulo se não há pedido.</summary>
    public DateTimeOffset? PasswordResetTokenExpiresAt { get; set; }

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
