using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Security;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Servers;

/// <summary>
///     Revoga um convite ainda não usado.
/// </summary>
public sealed class RevokeInvite(IInviteRepository invites, ICurrentUserScope scope)
{
    public async Task<Result> HandleAsync(Guid inviteId, CancellationToken ct)
    {
        var invite = await invites.GetByIdAsync(inviteId, ct);
        if (invite is null)
            return Result.Fail("Convite não encontrado.");

        var auth = await scope.RequireAsync(invite.GameServerId, ServerAccessPolicy.CanManageMembers, ct);
        if (!auth.Succeeded)
            return Result.Fail("Convite não encontrado.");

        if (invite.RedeemedAt is not null)
            return Result.Fail("Este convite já foi usado. Remova o membro para tirar o acesso.");

        invite.Revoke(DateTimeOffset.UtcNow);
        await invites.UpdateAsync(invite, ct);

        return Result.Success();
    }
}

/// <summary>
///     Tira o acesso de alguém a um servidor.
/// </summary>
public sealed class RemoveMember(
    IMembershipRepository memberships,
    IServerHubNotifier notifier,
    ICurrentUserScope scope)
{
    public async Task<Result> HandleAsync(Guid gameServerId, Guid userId, CancellationToken ct)
    {
        var auth = await scope.RequireAsync(gameServerId, ServerAccessPolicy.CanManageMembers, ct);
        if (!auth.Succeeded)
            return auth;

        var membership = await memberships.GetAsync(userId, gameServerId, ct);
        if (membership is null)
            return Result.Fail("Este usuário não é membro do servidor.");

        // Remover o próprio vínculo deixaria o servidor sem ninguém que possa
        // gerenciá-lo — e não há caminho de volta pela UI.
        if (userId == scope.UserId)
            return Result.Fail("Você não pode remover o próprio acesso.");

        if (membership.Role is ServerRole.Owner)
            return Result.Fail("O dono do servidor não pode ser removido.");

        await memberships.RemoveAsync(membership.Id, ct);

        // Papel nulo = perdeu o acesso. Avisar não é cortesia: é o que tira as
        // conexões dele do console agora, em vez de na próxima reconexão — que
        // seria quando o jogador quisesse.
        await notifier.NotifyRoleChangedAsync(gameServerId, userId, null, ct);

        return Result.Success();
    }
}

/// <summary>
///     Muda o papel de um membro.
/// </summary>
public sealed class ChangeMemberRole(
    IMembershipRepository memberships,
    IServerHubNotifier notifier,
    ICurrentUserScope scope)
{
    public async Task<Result> HandleAsync(
        Guid gameServerId, Guid userId, ServerRoleDto role, CancellationToken ct)
    {
        var auth = await scope.RequireAsync(gameServerId, ServerAccessPolicy.CanManageMembers, ct);
        if (!auth.Succeeded)
            return auth;

        if (role >= ServerRoleDto.Owner)
            return Result.Fail("Transferir a propriedade do servidor não é feito por aqui.");

        var membership = await memberships.GetAsync(userId, gameServerId, ct);
        if (membership is null)
            return Result.Fail("Este usuário não é membro do servidor.");

        // Mesma razão do RemoveMember: um Owner que se rebaixa sem querer perde
        // o servidor, e ninguém sobra para desfazer.
        if (userId == scope.UserId)
            return Result.Fail("Você não pode mudar o próprio papel.");

        if (membership.Role is ServerRole.Owner)
            return Result.Fail("O papel do dono não pode ser alterado.");

        membership.Role = role.ToDomain();
        await memberships.UpdateAsync(membership, ct);

        await notifier.NotifyRoleChangedAsync(gameServerId, userId, role, ct);

        return Result.Success();
    }
}
