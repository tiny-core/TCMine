using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Cloud;

/// <summary>As nuvens que o usuário enxerga: as dele, ou todas para o admin da instalação.</summary>
public sealed class ListCloudVaults(ICloudAdminRepository repo, ICurrentUserScope scope)
{
    public async Task<IReadOnlyList<CloudVaultSummary>> HandleAsync(CancellationToken ct)
    {
        if (scope.UserId is not { } userId)
            return [];
        return await repo.ListVaultsAsync(scope.IsInstanceAdmin ? null : userId, ct);
    }
}

/// <summary>Cria uma nuvem do usuário atual (ele vira o dono).</summary>
public sealed class CreateCloudVault(
    ICloudAdminRepository repo,
    ICloudGovernanceRepository governance,
    ICurrentUserScope scope)
{
    public async Task<Result<Guid>> HandleAsync(string name, CancellationToken ct)
    {
        if (scope.UserId is null)
            return Result<Guid>.Fail("Entre no painel para criar uma nuvem.");

        var trimmed = name.Trim();
        if (trimmed.Length is 0 or > CloudVault.NameMaxLength)
            return Result<Guid>.Fail($"O nome deve ter de 1 a {CloudVault.NameMaxLength} caracteres.");

        var vault = new CloudVault { Name = trimmed, OwnerId = scope.OwnerId };
        await repo.AddVaultAsync(vault, ct);
        await CloudAudit.WriteAsync(governance, vault.Id, scope.UserId, "vault.create", trimmed, ct);
        return Result<Guid>.Success(vault.Id);
    }
}

/// <summary>Configuração editável de uma nuvem, como o formulário do painel manda.</summary>
public sealed record CloudVaultSettings(
    string Name,
    bool IsEnabled,
    CloudPolicyMode PolicyMode,
    int LeaseTtlMinutes,
    int MaxItemBytes,
    int MaxChannelsPerPlayer,
    int MaxTypesPerChannel,
    long MaxTotalPerChannel);

public sealed class GetCloudVault(ICloudAdminRepository repo, ICurrentUserScope scope)
{
    public async Task<Result<CloudVault>> HandleAsync(Guid vaultId, CancellationToken ct) =>
        await CloudVaultAccess.RequireAsync(repo, scope, vaultId, ct);
}

/// <summary>
///     Salva nome, liga/desliga, modo da política e limites. Os limites são
///     validados JUNTOS pela entidade (<see cref="CloudVault.UpdateLimits" />):
///     um valor fora de faixa recusa o formulário inteiro.
/// </summary>
public sealed class UpdateCloudVault(
    ICloudAdminRepository repo,
    ICloudGovernanceRepository governance,
    ICurrentUserScope scope)
{
    public async Task<Result> HandleAsync(Guid vaultId, CloudVaultSettings settings, CancellationToken ct)
    {
        var access = await CloudVaultAccess.RequireAsync(repo, scope, vaultId, ct);
        if (!access.Succeeded)
            return Result.Fail(access.Error!);
        var vault = access.Value!;

        var name = settings.Name.Trim();
        if (name.Length is 0 or > CloudVault.NameMaxLength)
            return Result.Fail($"O nome deve ter de 1 a {CloudVault.NameMaxLength} caracteres.");

        try
        {
            vault.UpdateLimits(settings.LeaseTtlMinutes, settings.MaxItemBytes, settings.MaxChannelsPerPlayer,
                settings.MaxTypesPerChannel, settings.MaxTotalPerChannel);
        }
        catch (ArgumentOutOfRangeException e)
        {
            // A mensagem da entidade já está em português e diz qual campo.
            return Result.Fail(e.Message.Split(" (Parameter")[0]);
        }

        vault.Name = name;
        vault.SetPolicyMode(settings.PolicyMode);
        vault.SetEnabled(settings.IsEnabled);
        await repo.UpdateVaultAsync(vault, ct);
        await CloudAudit.WriteAsync(governance, vaultId, scope.UserId, "vault.update",
            $"{name}; {(settings.IsEnabled ? "ligada" : "somente leitura")}; {settings.PolicyMode}; TTL {settings.LeaseTtlMinutes} min; "
            + $"item {settings.MaxItemBytes} B; {settings.MaxChannelsPerPlayer} canais; {settings.MaxTypesPerChannel} tipos; "
            + $"{settings.MaxTotalPerChannel} itens", ct);
        return Result.Success();
    }
}
