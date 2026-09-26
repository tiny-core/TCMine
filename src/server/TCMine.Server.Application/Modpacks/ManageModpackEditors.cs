using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Security;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Quem edita um modpack. Leitura, mas autorizada como as escritas: a
///     lista de editores diz quem pode publicar em cima do trabalho de quem —
///     não é informação para qualquer conta autenticada.
/// </summary>
public sealed class ListModpackAccess(
    IModpackMembershipRepository memberships,
    ICurrentUserScope scope)
{
    public async Task<Result<IReadOnlyList<ModpackMemberView>>> HandleAsync(Guid modpackId, CancellationToken ct)
    {
        var auth = await scope.RequireAsync(modpackId, ModpackAccessPolicy.CanManageEditors, ct);
        if (!auth.Succeeded)
            return Result<IReadOnlyList<ModpackMemberView>>.Fail(auth.Error!);

        var membros = await memberships.ListWithUsersAsync(modpackId, ct);
        return Result<IReadOnlyList<ModpackMemberView>>.Success(membros);
    }
}

/// <summary>
///     Concede acesso de Editor a uma conta já existente na instalação.
///     Sem convite de propósito: ao contrário de um jogador chegando de fora
///     para um servidor, quem edita modpack já está no sistema — a única
///     pergunta é qual conta e não como ela chega até aqui.
/// </summary>
public sealed class AddModpackEditor(
    IModpackMembershipRepository memberships,
    IUserRepository users,
    ICurrentUserScope scope)
{
    public async Task<Result> HandleAsync(Guid modpackId, Guid userId, CancellationToken ct)
    {
        var auth = await scope.RequireAsync(modpackId, ModpackAccessPolicy.CanManageEditors, ct);
        if (!auth.Succeeded)
            return auth;

        if (await users.GetByIdAsync(userId, ct) is null)
            return Result.Fail("Usuário não encontrado.");

        var existing = await memberships.GetAsync(userId, modpackId, ct);
        if (existing is not null)
            return Result.Fail("Esta conta já tem acesso a este modpack.");

        await memberships.AddAsync(
            new ModpackMembership { UserId = userId, ModpackId = modpackId, Role = ModpackRole.Editor },
            ct);

        return Result.Success();
    }
}

/// <summary>Tira o acesso de alguém a um modpack.</summary>
public sealed class RemoveModpackEditor(
    IModpackMembershipRepository memberships,
    ICurrentUserScope scope)
{
    public async Task<Result> HandleAsync(Guid modpackId, Guid userId, CancellationToken ct)
    {
        var auth = await scope.RequireAsync(modpackId, ModpackAccessPolicy.CanManageEditors, ct);
        if (!auth.Succeeded)
            return auth;

        var membership = await memberships.GetAsync(userId, modpackId, ct);
        if (membership is null)
            return Result.Fail("Esta conta não tem acesso a este modpack.");

        // Mesma razão do RemoveMember de servidor: remover o próprio vínculo
        // deixaria o modpack sem ninguém que possa gerenciá-lo, sem caminho de
        // volta pela UI.
        if (userId == scope.UserId)
            return Result.Fail("Você não pode remover o próprio acesso.");

        if (membership.Role is ModpackRole.Owner)
            return Result.Fail("O dono do modpack não pode ser removido.");

        await memberships.RemoveAsync(membership.Id, ct);
        return Result.Success();
    }
}
