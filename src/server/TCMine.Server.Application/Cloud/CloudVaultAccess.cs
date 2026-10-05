using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Cloud;

/// <summary>
///     Quem pode mexer numa nuvem pelo painel: o dono dela ou o admin da
///     instalação. Num lugar só porque toda tela da nuvem começa por aqui, e uma
///     checagem esquecida numa delas seria um dono vendo os jogadores do outro.
///     "Não encontrada" e "sem acesso" dão a mesma resposta de propósito: não
///     confirmar que uma nuvem alheia existe.
/// </summary>
public static class CloudVaultAccess
{
    public const string NotFound = "Nuvem não encontrada.";

    public static async Task<Result<CloudVault>> RequireAsync(ICloudAdminRepository repo, ICurrentUserScope scope,
        Guid vaultId, CancellationToken ct)
    {
        if (scope.UserId is not { } userId)
            return Result<CloudVault>.Fail(NotFound);

        var vault = await repo.GetVaultAsync(vaultId, ct);
        return vault is not null && (scope.IsInstanceAdmin || vault.OwnerId == userId)
            ? Result<CloudVault>.Success(vault)
            : Result<CloudVault>.Fail(NotFound);
    }
}
