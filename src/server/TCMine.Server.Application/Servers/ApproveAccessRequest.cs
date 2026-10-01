using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Security;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Servers;

/// <summary>
///     Aprova um pedido: cria o Membership (igual <see cref="RedeemInvite" />
///     cria ao resgatar um convite — os dois caminhos chegam no mesmo lugar) e
///     sincroniza a whitelist do jogo, senão o jogador vê o servidor liberado
///     no painel e "not white-listed" ao tentar entrar.
/// </summary>
public sealed class ApproveAccessRequest(
    IAccessRequestRepository requests,
    IMembershipRepository memberships,
    IServerWhitelistSync whitelist,
    ICurrentUserScope scope)
{
    public async Task<Result> HandleAsync(Guid requestId, CancellationToken ct)
    {
        if (scope.UserId is not { } resolvedBy)
            return Result.Fail("Entre na sua conta antes de decidir um pedido.");

        var request = await requests.GetByIdAsync(requestId, ct);
        if (request is null)
            return Result.Fail("Pedido não encontrado.");

        if (!await PodeDecidirAsync(request.GameServerId, ct))
            return Result.Fail("Sem permissão para decidir pedidos deste servidor.");

        try
        {
            request.Approve(resolvedBy, DateTimeOffset.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Fail(ex.Message);
        }

        // Member: o mesmo papel mínimo que um convite comum concede — pedir
        // acesso não deveria dar mais poder do que ser convidado dá.
        var existente = await memberships.GetAsync(request.UserId, request.GameServerId, ct);
        if (existente is null)
        {
            await memberships.AddAsync(
                new Membership { UserId = request.UserId, GameServerId = request.GameServerId, Role = ServerRole.Member },
                ct);
        }

        await requests.UpdateAsync(request, ct);
        await whitelist.HandleAsync(request.GameServerId, ct);

        return Result.Success();
    }

    private async Task<bool> PodeDecidirAsync(Guid gameServerId, CancellationToken ct) =>
        scope.IsInstanceAdmin || AccessRequestPolicy.CanDecide(await scope.GetRoleAsync(gameServerId, ct));
}
