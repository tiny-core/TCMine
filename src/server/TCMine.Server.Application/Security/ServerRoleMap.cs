using TCMine.Contracts.Servers;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Security;

/// <summary>
///     Tradução entre o papel do domínio e o do contrato.
///     Explícita, e não um cast: os dois enums têm hoje os mesmos valores, mas
///     são de camadas diferentes e evoluem por motivos diferentes. No dia em que
///     um deles ganhar um papel intermediário, o cast silencioso passaria a
///     converter Moderator em outra coisa sem nada acusar — e o resultado disso
///     é alguém recebendo permissão que não foi concedida.
/// </summary>
public static class ServerRoleMap
{
    public static ServerRoleDto ToDto(this ServerRole role) => role switch
    {
        ServerRole.Member => ServerRoleDto.Member,
        ServerRole.Moderator => ServerRoleDto.Moderator,
        ServerRole.Admin => ServerRoleDto.Admin,
        ServerRole.Owner => ServerRoleDto.Owner,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Papel de servidor desconhecido.")
    };

    public static ServerRole ToDomain(this ServerRoleDto role) => role switch
    {
        ServerRoleDto.Member => ServerRole.Member,
        ServerRoleDto.Moderator => ServerRole.Moderator,
        ServerRoleDto.Admin => ServerRole.Admin,
        ServerRoleDto.Owner => ServerRole.Owner,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Papel de servidor desconhecido.")
    };
}
