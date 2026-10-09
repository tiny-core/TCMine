using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Cloud;

public sealed class ListCloudDoubtful(
    ICloudAdminRepository repo,
    ICloudGovernanceRepository governance,
    ICurrentUserScope scope)
{
    public async Task<Result<IReadOnlyList<CloudDoubtfulOperation>>> HandleAsync(Guid vaultId, bool openOnly,
        CancellationToken ct)
    {
        var access = await CloudVaultAccess.RequireAsync(repo, scope, vaultId, ct);
        return access.Succeeded
            ? Result<IReadOnlyList<CloudDoubtfulOperation>>.Success(
                await governance.ListDoubtfulAsync(vaultId, openOnly, ct))
            : Result<IReadOnlyList<CloudDoubtfulOperation>>.Fail(access.Error!);
    }
}

/// <summary>
///     Decide uma operação em dúvida. Devolver: os itens VOLTAM para a nuvem (nos
///     dois tipos — crédito que não chegou e débito que talvez não chegou ao
///     mundo, o jogador perdeu os itens). Dispensar: nada muda. O dono decide
///     olhando o mundo (o jogador tem os itens?) — devolver sem olhar duplica.
/// </summary>
public sealed class ResolveCloudDoubtful(
    ICloudAdminRepository repo,
    ICloudGovernanceRepository governance,
    ICloudStorageRepository storage,
    ICurrentUserScope scope,
    TimeProvider clock)
{
    public async Task<Result> HandleAsync(Guid vaultId, Guid operationId, bool refund, CancellationToken ct)
    {
        var access = await CloudVaultAccess.RequireAsync(repo, scope, vaultId, ct);
        if (!access.Succeeded)
            return Result.Fail(access.Error!);

        var op = await governance.GetDoubtfulAsync(operationId, ct);
        if (op is null || op.VaultId != vaultId)
            return Result.Fail("Operação não encontrada.");
        if (!op.IsOpen)
            return Result.Fail("Esta operação já foi resolvida.");

        var userId = scope.UserId!.Value;
        var now = clock.GetUtcNow();
        if (!refund)
        {
            op.Resolve(CloudDoubtfulStatus.Dismissed, userId, now);
            await governance.UpdateDoubtfulAsync(op, ct);
            await CloudAudit.WriteAsync(governance, vaultId, userId, "doubtful.dismiss", Describe(op), ct);
            return Result.Success();
        }

        var channel = await repo.GetChannelAsync(op.ChannelId, ct);
        if (channel is null || channel.VaultId != vaultId || channel.PlayerUuid != op.PlayerUuid)
            return Result.Fail("O canal da operação não existe mais.");
        var itemIds = await storage.ItemIdsByFingerprintAsync([op.Fingerprint], ct);
        if (!itemIds.TryGetValue(op.Fingerprint, out var itemTypeId))
        {
            return Result.Fail(
                "O TCMine nunca recebeu a definição deste item (o crédito não chegou); não dá para devolver por aqui.");
        }

        var (leases, leaseError) = await CloudLeaseGuard.LoadFreeAsync(storage, vaultId, [op.PlayerUuid], now, ct);
        if (leases is null)
            return Result.Fail(leaseError!);

        var current = (await storage.ListBalancesAsync([channel.Id], ct))
            .FirstOrDefault(b => b.ItemTypeId == itemTypeId)?.Amount ?? 0;
        op.Resolve(CloudDoubtfulStatus.Refunded, userId, now);
        var committed = await storage.CommitAdminAsync(new CloudAdminCommit(leases, [],
            [new CloudAdminChange(channel.Id, itemTypeId, op.Amount, current + op.Amount, null)],
            CloudLedgerSource.Admin, userId, $"Devolução de operação em dúvida: {Describe(op)}", [op]), ct);
        if (!committed)
            return Result.Fail("Um servidor pegou os canais deste jogador agora; tente de novo.");

        await CloudAudit.WriteAsync(governance, vaultId, userId, "doubtful.refund", Describe(op), ct);
        return Result.Success();
    }

    private static string Describe(CloudDoubtfulOperation op) =>
        $"{op.Kind} {op.Amount} de {op.Fingerprint[..12]}… para {op.PlayerUuid}";
}
