using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Security;

/// <summary>
///     Liga ou desliga o admin da instalação numa conta.
///     A guarda que importa é a de baixo: tirar o ÚLTIMO admin deixaria a
///     instalação sem ninguém que possa gerenciar usuários, servidores
///     alheios ou configurações — um estado de que não existe volta pela UI.
/// </summary>
public sealed class SetInstanceAdmin(IUserRepository users, ICurrentUserScope scope)
{
    public async Task<Result> HandleAsync(Guid userId, bool isAdmin, CancellationToken ct)
    {
        if (!scope.IsInstanceAdmin)
            return Result.Fail("Só o admin da instalação gerencia usuários.");

        var user = await users.GetByIdAsync(userId, ct);
        if (user is null)
            return Result.Fail("Usuário não encontrado.");

        if (!isAdmin && user.IsInstanceAdmin)
        {
            var todos = await users.ListAsync(ct);
            if (todos.Count(u => u.IsInstanceAdmin) <= 1)
                return Result.Fail("A instalação precisa de pelo menos um admin — promova outra conta antes.");
        }

        user.IsInstanceAdmin = isAdmin;
        await users.UpdateAsync(user, ct);
        return Result.Success();
    }
}
