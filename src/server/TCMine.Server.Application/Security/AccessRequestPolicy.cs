using TCMine.Contracts.Servers;

namespace TCMine.Server.Application.Security;

/// <summary>
///     Quem pode decidir um pedido de acesso. Usado por
///     <c>ApproveAccessRequest</c> e <c>DenyAccessRequest</c> — mesma regra nos
///     dois, porque aprovar e recusar são a mesma decisão vista pelos dois lados.
/// </summary>
public static class AccessRequestPolicy
{
    public static bool CanDecide(ServerRoleDto? role) => role is ServerRoleDto.Admin or ServerRoleDto.Owner;
}
