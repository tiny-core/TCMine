using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Security;

namespace TCMine.Server.Application.Servers;

public sealed class DenyAccessRequest(
    IAccessRequestRepository requests,
    ICurrentUserScope scope)
{
    public async Task<Result> HandleAsync(Guid requestId, CancellationToken ct)
    {
        if (scope.UserId is not { } resolvedBy)
            return Result.Fail("Entre na sua conta antes de decidir um pedido.");

        var request = await requests.GetByIdAsync(requestId, ct);
        if (request is null)
            return Result.Fail("Pedido não encontrado.");

        var podeDecidir = scope.IsInstanceAdmin
                           || AccessRequestPolicy.CanDecide(await scope.GetRoleAsync(request.GameServerId, ct));

        if (!podeDecidir)
            return Result.Fail("Sem permissão para decidir pedidos deste servidor.");

        try
        {
            request.Deny(resolvedBy, DateTimeOffset.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Fail(ex.Message);
        }

        await requests.UpdateAsync(request, ct);
        return Result.Success();
    }
}
