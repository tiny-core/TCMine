using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Servers;

/// <summary>
///     Jogador pedindo acesso a um servidor com whitelist — o caminho inverso
///     do convite (<see cref="RedeemInvite" />): aqui é o jogador puxando, não
///     o dono empurrando um código.
/// </summary>
public sealed class RequestServerAccess(
    IServerRepository servers,
    IMembershipRepository memberships,
    IAccessRequestRepository requests,
    ICurrentUserScope scope)
{
    public async Task<Result> HandleAsync(Guid gameServerId, CancellationToken ct)
    {
        if (scope.UserId is not { } userId)
            return Result.Fail("Entre na sua conta antes de pedir acesso.");

        if (await servers.GetByIdAsync(gameServerId, ct) is null)
            return Result.Fail("Servidor não encontrado.");

        // Já tem acesso: pedir de novo não faz sentido, e a tela nem deveria
        // oferecer o botão neste caso — mas quem chama o Hub direto não passa
        // pela tela.
        if (await memberships.GetAsync(userId, gameServerId, ct) is not null)
            return Result.Fail("Você já tem acesso a este servidor.");

        // Idempotente: um segundo clique (ou uma reconexão que reenvia o
        // pedido) não cria uma segunda linha — só confirma que já está pendente.
        if (await requests.GetPendingAsync(userId, gameServerId, ct) is not null)
            return Result.Success();

        await requests.AddAsync(
            new AccessRequest { UserId = userId, GameServerId = gameServerId }, ct);

        return Result.Success();
    }
}
