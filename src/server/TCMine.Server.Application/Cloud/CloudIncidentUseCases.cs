using System.Text.Json;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Cloud;

public sealed class ListCloudIncidents(ICloudAdminRepository repo, ICloudGovernanceRepository governance, ICurrentUserScope scope)
{
    public async Task<Result<IReadOnlyList<CloudRollbackIncident>>> HandleAsync(Guid vaultId, bool openOnly, CancellationToken ct)
    {
        var access = await CloudVaultAccess.RequireAsync(repo, scope, vaultId, ct);
        return access.Succeeded
            ? Result<IReadOnlyList<CloudRollbackIncident>>.Success(await governance.ListIncidentsAsync(vaultId, openOnly, ct))
            : Result<IReadOnlyList<CloudRollbackIncident>>.Fail(access.Error!);
    }
}

/// <summary>Uma linha da prévia do estorno: o que volta e onde o saldo não cobre.</summary>
/// <param name="Shortfall">quanto NÃO pôde ser estornado porque o saldo acabou (itens já saíram por outro servidor)</param>
public sealed record CloudRevertLine(string PlayerUuid, Guid ChannelId, Guid ItemTypeId, string ItemId, string DisplayName,
    long Delta, long Current, long After, long Shortfall);

/// <summary>
///     Estorno de um mundo que voltou no tempo (plano §5.2). Os lotes que o
///     servidor aplicou DEPOIS do checkpoint do mundo restaurado viram linhas
///     inversas no ledger (origem "Revert"). Se o jogador já levou parte desses
///     itens para outro servidor, o saldo para em zero e a diferença fica
///     registrada (é a duplicação que o rollback causou e que não há como
///     desfazer daqui) — a prévia mostra isso antes de confirmar.
///     Aceitar: nada é estornado; o dono assume que o mundo restaurado vale.
/// </summary>
public sealed class ResolveCloudIncident(
    ICloudAdminRepository repo,
    ICloudGovernanceRepository governance,
    ICloudStorageRepository storage,
    ICurrentUserScope scope,
    TimeProvider clock)
{
    public async Task<Result<IReadOnlyList<CloudRevertLine>>> PreviewAsync(Guid vaultId, Guid incidentId, CancellationToken ct)
    {
        var load = await LoadAsync(vaultId, incidentId, ct);
        return load.Error is not null
            ? Result<IReadOnlyList<CloudRevertLine>>.Fail(load.Error)
            : Result<IReadOnlyList<CloudRevertLine>>.Success(load.Lines!);
    }

    public async Task<Result> HandleAsync(Guid vaultId, Guid incidentId, bool revert, CancellationToken ct)
    {
        var load = await LoadAsync(vaultId, incidentId, ct);
        if (load.Error is not null)
            return Result.Fail(load.Error);

        var incident = load.Incident!;
        var userId = scope.UserId!.Value;
        var now = clock.GetUtcNow();
        if (!revert)
        {
            incident.Resolve(CloudIncidentStatus.Accepted, userId, now);
            await governance.UpdateIncidentAsync(incident, ct);
            await CloudAudit.WriteAsync(governance, vaultId, userId, "incident.accept", incident.Detail, ct);
            return Result.Success();
        }

        var lines = load.Lines!;
        // Leases presos pelo próprio servidor do incidente são liberados junto: ele
        // está em somente leitura e vai pegar os saldos novos no próximo acquire.
        var (leases, leaseError) = await CloudLeaseGuard.LoadFreeAsync(storage, vaultId,
            load.Batches!.Select(b => b.PlayerUuid), now, ct, releasableBy: incident.ServerId);
        if (leases is null)
            return Result.Fail(leaseError!);

        foreach (var batch in load.Batches!)
            batch.MarkReverted();
        incident.Resolve(CloudIncidentStatus.Reverted, userId, now);

        var shortfall = lines.Sum(l => l.Shortfall);
        var reason = $"Estorno do rollback do servidor {incident.ServerId} ({load.Batches.Count} lotes"
                     + (shortfall > 0 ? $"; {shortfall} itens já tinham saído e não voltaram" : "") + ")";
        var committed = await storage.CommitAdminAsync(new CloudAdminCommit(leases, [],
            [.. lines.Where(l => l.Delta != 0).Select(l => new CloudAdminChange(l.ChannelId, l.ItemTypeId, l.Delta, l.After, null))],
            CloudLedgerSource.Revert, userId, reason, [incident, .. load.Batches]), ct);
        if (!committed)
            return Result.Fail("Um servidor pegou os canais de um destes jogadores agora; tente de novo.");

        await CloudAudit.WriteAsync(governance, vaultId, userId, "incident.revert", reason, ct);
        return Result.Success();
    }

    private sealed record Loaded(string? Error, CloudRollbackIncident? Incident = null,
        IReadOnlyList<CloudBatch>? Batches = null, IReadOnlyList<CloudRevertLine>? Lines = null);

    private async Task<Loaded> LoadAsync(Guid vaultId, Guid incidentId, CancellationToken ct)
    {
        var access = await CloudVaultAccess.RequireAsync(repo, scope, vaultId, ct);
        if (!access.Succeeded)
            return new Loaded(access.Error);

        var incident = await governance.GetIncidentAsync(incidentId, ct);
        if (incident is null || incident.VaultId != vaultId)
            return new Loaded("Incidente não encontrado.");
        if (!incident.IsOpen)
            return new Loaded("Este incidente já foi resolvido.");

        var checkpoint = JsonSerializer.Deserialize<CloudCheckpoint>(incident.CheckpointJson);
        var declared = (checkpoint?.Players ?? new Dictionary<string, CloudSeqPosition>())
            .Select(kv => (Player: CloudChannel.NormalizePlayerUuid(kv.Key), kv.Value))
            .Where(x => x.Player is not null)
            .ToDictionary(x => x.Player!, x => (x.Value.Epoch, x.Value.Seq));

        var batches = (await storage.ListAppliedBatchesAsync(vaultId, incident.ServerId, ct))
            .Where(b => (b.Epoch, b.Seq).CompareTo(declared.GetValueOrDefault(b.PlayerUuid)) > 0)
            .ToList();
        var ledger = await storage.ListLedgerByBatchesAsync([.. batches.Select(b => b.Id)], ct);
        var playerOf = batches.ToDictionary(b => b.Id, b => b.PlayerUuid);

        var totals = ledger
            .GroupBy(e => (e.ChannelId, e.ItemTypeId))
            .Select(g => (g.Key.ChannelId, g.Key.ItemTypeId, Player: playerOf[g.First().BatchId!.Value], Revert: -g.Sum(e => e.Delta)))
            .ToList();
        var balances = (await storage.ListBalancesAsync([.. totals.Select(t => t.ChannelId).Distinct()], ct))
            .ToDictionary(b => (b.ChannelId, b.ItemTypeId), b => b.Amount);
        var names = await governance.ItemNamesAsync([.. totals.Select(t => t.ItemTypeId).Distinct()], ct);

        var lines = totals.Select(t =>
        {
            var current = balances.GetValueOrDefault((t.ChannelId, t.ItemTypeId));
            var wanted = current + t.Revert;
            var delta = wanted < 0 ? -current : t.Revert;
            var name = names.TryGetValue(t.ItemTypeId, out var n) ? n : (ItemId: "?", DisplayName: "?");
            return new CloudRevertLine(t.Player, t.ChannelId, t.ItemTypeId, name.ItemId, name.DisplayName, delta,
                current, current + delta, wanted < 0 ? -wanted : 0);
        }).ToList();

        return new Loaded(null, incident, batches, lines);
    }
}

public sealed class ListCloudAudit(ICloudAdminRepository repo, ICloudGovernanceRepository governance, ICurrentUserScope scope)
{
    public const int Limit = 300;

    public async Task<Result<(IReadOnlyList<CloudAdminAuditEntry> Actions, IReadOnlyList<CloudLedgerView> Ledger)>> HandleAsync(
        Guid vaultId, string? playerUuid, CancellationToken ct)
    {
        var access = await CloudVaultAccess.RequireAsync(repo, scope, vaultId, ct);
        if (!access.Succeeded)
            return Result<(IReadOnlyList<CloudAdminAuditEntry>, IReadOnlyList<CloudLedgerView>)>.Fail(access.Error!);

        var player = string.IsNullOrWhiteSpace(playerUuid) ? null : CloudChannel.NormalizePlayerUuid(playerUuid);
        return Result<(IReadOnlyList<CloudAdminAuditEntry>, IReadOnlyList<CloudLedgerView>)>.Success((
            await governance.ListAuditAsync(vaultId, Limit, ct),
            await governance.ListLedgerAsync(vaultId, player, Limit, ct)));
    }
}
