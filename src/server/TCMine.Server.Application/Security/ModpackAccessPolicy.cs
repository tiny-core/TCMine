using TCMine.Contracts.Modpacks;

namespace TCMine.Server.Application.Security;

/// <summary>
///     Privilégios sobre um modpack. Ao lado da <see cref="ServerAccessPolicy" />
///     e pela mesma razão: "quem pode o quê" mora num lugar só, testável sem UI.
/// </summary>
public static class ModpackAccessPolicy
{
    /// <summary>
    ///     Editar mods, overrides, criar e publicar versões, mexer no ícone.
    ///     Todo o dia a dia de manter o pack em cima.
    /// </summary>
    public static bool CanEdit(ModpackRoleDto role) => role >= ModpackRoleDto.Editor;

    /// <summary>
    ///     Apagar o modpack. Só Owner: é a única ação sem volta — leva junto o
    ///     histórico de versões publicadas.
    /// </summary>
    public static bool CanDelete(ModpackRoleDto role) => role >= ModpackRoleDto.Owner;

    /// <summary>
    ///     Conceder e revogar acesso de edição a outra conta. Owner porque quem
    ///     gerencia editores pode se autopromover — conceder isso a um Editor
    ///     tornaria a distinção entre os dois papéis decorativa.
    /// </summary>
    public static bool CanManageEditors(ModpackRoleDto role) => role >= ModpackRoleDto.Owner;
}
