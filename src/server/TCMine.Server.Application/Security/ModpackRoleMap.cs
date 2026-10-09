using TCMine.Contracts.Modpacks;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Security;

/// <summary>
///     Tradução entre o papel do domínio e o do contrato — ver o porquê em
///     <see cref="ServerRoleMap" />, a mesma decisão vale aqui.
/// </summary>
public static class ModpackRoleMap
{
    public static ModpackRoleDto ToDto(this ModpackRole role) => role switch
    {
        ModpackRole.Editor => ModpackRoleDto.Editor,
        ModpackRole.Owner => ModpackRoleDto.Owner,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Papel de modpack desconhecido.")
    };
}
