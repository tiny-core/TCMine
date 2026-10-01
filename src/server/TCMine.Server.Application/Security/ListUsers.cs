using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Security;

/// <summary>
///     Todas as contas da instalação — a base para o painel "Usuários", e para
///     o projeto da nuvem de itens mais à frente (ver docs/CLOUD-STORAGE.md),
///     que vai precisar achar jogador por nome/UUID sobre a mesma tabela.
///     Só o admin da instalação vê: nome, tipo de conta e última vez visto de
///     todo mundo não é informação de membro comum.
/// </summary>
public sealed class ListUsers(IUserRepository users, ICurrentUserScope scope)
{
    public async Task<Result<IReadOnlyList<User>>> HandleAsync(CancellationToken ct)
    {
        if (!scope.IsInstanceAdmin)
            return Result<IReadOnlyList<User>>.Fail("Só o admin da instalação gerencia usuários.");

        return Result<IReadOnlyList<User>>.Success(await users.ListAsync(ct));
    }
}
