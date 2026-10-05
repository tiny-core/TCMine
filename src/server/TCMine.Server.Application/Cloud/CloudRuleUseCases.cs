using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Cloud;

public sealed class ListCloudRules(ICloudAdminRepository repo, ICloudGovernanceRepository governance, ICurrentUserScope scope)
{
    public async Task<Result<IReadOnlyList<CloudItemRule>>> HandleAsync(Guid vaultId, CancellationToken ct)
    {
        var access = await CloudVaultAccess.RequireAsync(repo, scope, vaultId, ct);
        return access.Succeeded
            ? Result<IReadOnlyList<CloudItemRule>>.Success(await governance.ListRulesAsync(vaultId, ct))
            : Result<IReadOnlyList<CloudItemRule>>.Fail(access.Error!);
    }
}

/// <summary>
///     Cria uma regra de item e aumenta a versão da política: os servidores
///     buscam a política nova no próximo heartbeat.
/// </summary>
public sealed class AddCloudRule(ICloudAdminRepository repo, ICloudGovernanceRepository governance, ICurrentUserScope scope)
{
    public async Task<Result> HandleAsync(Guid vaultId, CloudRuleScope ruleScope, string pattern, CloudRuleAction action,
        string? note, CancellationToken ct)
    {
        var access = await CloudVaultAccess.RequireAsync(repo, scope, vaultId, ct);
        if (!access.Succeeded)
            return Result.Fail(access.Error!);

        var normalized = CloudItemRule.NormalizePattern(ruleScope, pattern);
        if (normalized is null)
            return Result.Fail(ruleScope == CloudRuleScope.Mod
                ? "Mod: só o id do mod (ex.: refinedstorage)."
                : "Use mod:nome (ex.: minecraft:shulker_box ou c:shulker_boxes para tag).");

        var added = await governance.AddRuleAsync(new CloudItemRule
        {
            VaultId = vaultId, Scope = ruleScope, Pattern = normalized, Action = action,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()[..Math.Min(note.Trim().Length, 256)],
            CreatedByUserId = scope.UserId
        }, ct);
        if (!added)
            return Result.Fail("Já existe uma regra para esse alvo; apague a antiga antes.");

        var vault = access.Value!;
        vault.BumpPolicyVersion();
        await repo.UpdateVaultAsync(vault, ct);
        await CloudAudit.WriteAsync(governance, vaultId, scope.UserId, "rule.add", $"{action} {ruleScope} {normalized}", ct);
        return Result.Success();
    }
}

public sealed class RemoveCloudRule(ICloudAdminRepository repo, ICloudGovernanceRepository governance, ICurrentUserScope scope)
{
    public async Task<Result> HandleAsync(Guid vaultId, Guid ruleId, CancellationToken ct)
    {
        var access = await CloudVaultAccess.RequireAsync(repo, scope, vaultId, ct);
        if (!access.Succeeded)
            return Result.Fail(access.Error!);

        var rule = await governance.GetRuleAsync(ruleId, ct);
        if (rule is null || rule.VaultId != vaultId)
            return Result.Fail("Regra não encontrada.");

        await governance.RemoveRuleAsync(ruleId, ct);
        var vault = access.Value!;
        vault.BumpPolicyVersion();
        await repo.UpdateVaultAsync(vault, ct);
        await CloudAudit.WriteAsync(governance, vaultId, scope.UserId, "rule.remove",
            $"{rule.Action} {rule.Scope} {rule.Pattern}", ct);
        return Result.Success();
    }
}

public sealed class ListCloudSuspects(ICloudAdminRepository repo, ICloudGovernanceRepository governance, ICurrentUserScope scope)
{
    public async Task<Result<IReadOnlyList<CloudSuspectItem>>> HandleAsync(Guid vaultId, bool pendingOnly, CancellationToken ct)
    {
        var access = await CloudVaultAccess.RequireAsync(repo, scope, vaultId, ct);
        return access.Succeeded
            ? Result<IReadOnlyList<CloudSuspectItem>>.Success(
                await governance.ListSuspectsAsync(vaultId, pendingOnly ? CloudSuspectStatus.Pending : null, ct))
            : Result<IReadOnlyList<CloudSuspectItem>>.Fail(access.Error!);
    }
}

/// <summary>
///     Decide um suspeito: permitir (regra Allow do item) ou bloquear de vez
///     (regra Block). A regra é o que o mod aplica; a marcação do suspeito é só
///     para ele sair da fila.
/// </summary>
public sealed class ResolveCloudSuspect(
    ICloudAdminRepository repo,
    ICloudGovernanceRepository governance,
    AddCloudRule addRule,
    ICurrentUserScope scope)
{
    public async Task<Result> HandleAsync(Guid vaultId, Guid suspectId, bool allow, CancellationToken ct)
    {
        var access = await CloudVaultAccess.RequireAsync(repo, scope, vaultId, ct);
        if (!access.Succeeded)
            return Result.Fail(access.Error!);

        var suspect = await governance.GetSuspectAsync(suspectId, ct);
        if (suspect is null || suspect.VaultId != vaultId)
            return Result.Fail("Item não encontrado.");

        // Já existir uma regra para o item não é erro aqui: o dono só está
        // limpando a fila.
        await addRule.HandleAsync(vaultId, CloudRuleScope.Item, suspect.ItemId,
            allow ? CloudRuleAction.Allow : CloudRuleAction.Block, "Decidido na fila de suspeitos", ct);
        suspect.Resolve(allow ? CloudSuspectStatus.Allowed : CloudSuspectStatus.Blocked, scope.UserId!.Value);
        await governance.UpdateSuspectAsync(suspect, ct);
        return Result.Success();
    }
}
