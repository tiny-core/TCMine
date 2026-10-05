using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Cloud;

/// <summary>Política de itens de uma nuvem no formato que o mod aplica.</summary>
public static class CloudPolicy
{
    public static async Task<CloudPolicyReply> LoadAsync(ICloudGovernanceRepository governance, CloudVault vault,
        CancellationToken ct)
    {
        var rules = await governance.ListRulesAsync(vault.Id, ct);
        return new CloudPolicyReply(vault.PolicyMode.ToString(), vault.PolicyVersion,
            [.. rules.Select(r => new CloudRuleDto(r.Scope.ToString(), r.Pattern, r.Action.ToString()))]);
    }
}

/// <summary><c>POST /policy</c>: o mod busca a política quando o heartbeat avisa que a versão mudou.</summary>
public sealed class GetCloudPolicy(ICloudCredentialRepository credentials, ICloudGovernanceRepository governance)
{
    public async Task<CloudCallResult<CloudPolicyReply>> HandleAsync(CloudServerContext ctx, CancellationToken ct)
    {
        var vault = await credentials.GetVaultAsync(ctx.VaultId, ct);
        return vault is null
            ? CloudCallResult<CloudPolicyReply>.Forbidden("Nuvem não encontrada.")
            : CloudCallResult<CloudPolicyReply>.Ok(await CloudPolicy.LoadAsync(governance, vault, ct));
    }
}
