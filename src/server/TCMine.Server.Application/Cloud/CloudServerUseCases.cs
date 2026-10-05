using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Security;

namespace TCMine.Server.Application.Cloud;

/// <summary>Um servidor do dono da nuvem, como a aba "Servidores" mostra.</summary>
/// <param name="AttachedHere">ligado a ESTA nuvem</param>
/// <param name="AttachedElsewhere">ligado a outra nuvem do mesmo dono</param>
/// <param name="Key">chave ativa (só o prefixo e o último contato), se houver</param>
public sealed record CloudVaultServerView(Guid ServerId, string Name, bool AttachedHere, bool AttachedElsewhere,
    CloudServerKeyView? Key);

/// <summary>
///     Os servidores que podem usar a nuvem: os do MESMO dono dela (a regra do
///     domínio, <c>GameServer.AttachToCloudVault</c>). Servidores de outros
///     donos nem aparecem.
/// </summary>
public sealed class ListCloudVaultServers(ICloudAdminRepository repo, IServerRepository servers, ICurrentUserScope scope)
{
    public async Task<Result<IReadOnlyList<CloudVaultServerView>>> HandleAsync(Guid vaultId, CancellationToken ct)
    {
        var access = await CloudVaultAccess.RequireAsync(repo, scope, vaultId, ct);
        if (!access.Succeeded)
            return Result<IReadOnlyList<CloudVaultServerView>>.Fail(access.Error!);

        var owned = (await servers.ListAllAsync(ct)).Where(s => s.OwnerId == access.Value!.OwnerId).ToList();
        var keys = (await repo.ListActiveKeysAsync([.. owned.Select(s => s.Id)], ct)).ToDictionary(k => k.ServerId);

        IReadOnlyList<CloudVaultServerView> views =
        [
            .. owned.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).Select(s => new CloudVaultServerView(
                s.Id, s.Name,
                AttachedHere: s.CloudVaultId == vaultId,
                AttachedElsewhere: s.CloudVaultId is not null && s.CloudVaultId != vaultId,
                Key: s.CloudVaultId == vaultId ? keys.GetValueOrDefault(s.Id) : null))
        ];
        return Result<IReadOnlyList<CloudVaultServerView>>.Success(views);
    }
}

/// <summary>
///     Liga um servidor à nuvem ou desliga (<paramref name="vaultId" /> nulo).
///     Em qualquer troca, as chaves do servidor são revogadas: a chave vale para
///     UMA nuvem, e uma chave velha apontando para a nuvem anterior não pode
///     continuar gravando lá.
/// </summary>
public sealed class SetServerCloudVault(
    ICloudAdminRepository repo,
    IServerRepository servers,
    ICloudCredentialRepository credentials,
    ICloudServerFiles files,
    ICloudGovernanceRepository governance,
    ICurrentUserScope scope,
    TimeProvider clock)
{
    public async Task<Result> HandleAsync(Guid serverId, Guid? vaultId, CancellationToken ct)
    {
        var auth = await scope.RequireAsync(serverId, ServerAccessPolicy.CanManageCloud, ct);
        if (!auth.Succeeded)
            return auth;

        var server = await servers.GetByIdAsync(serverId, ct);
        if (server is null)
            return Result.Fail("Servidor não encontrado.");
        if (server.CloudVaultId == vaultId)
            return Result.Success();
        var previous = server.CloudVaultId;

        if (vaultId is { } id)
        {
            var access = await CloudVaultAccess.RequireAsync(repo, scope, id, ct);
            if (!access.Succeeded)
                return Result.Fail(access.Error!);
            try
            {
                server.AttachToCloudVault(access.Value!);
            }
            catch (InvalidOperationException e)
            {
                return Result.Fail(e.Message);
            }
        }
        else
        {
            server.DetachFromCloudVault();
        }

        // A chave e o arquivo saem já; a chave nova (da nuvem nova) vem no
        // próximo start do servidor (ProvisionServerCloudKey).
        await CloudKeyRotation.RevokeAllAsync(credentials, clock, serverId, ct);
        await files.DeleteAsync(serverId, ct);
        await servers.UpdateAsync(server, ct);

        // Registrado nas duas nuvens envolvidas: cada dono vê a sua história inteira.
        if (previous is { } from)
            await CloudAudit.WriteAsync(governance, from, scope.UserId, "server.detach", $"{server.Name} ({serverId})", ct);
        if (vaultId is { } to)
            await CloudAudit.WriteAsync(governance, to, scope.UserId, "server.attach", $"{server.Name} ({serverId})", ct);
        return Result.Success();
    }
}

/// <summary>
///     Emergência: revoga a chave do servidor e apaga o arquivo dela. A próxima
///     requisição dele à nuvem recebe 401; a nuvem volta no próximo start (chave
///     nova), a não ser que o servidor seja desligado da nuvem.
/// </summary>
public sealed class RevokeCloudServerKey(
    IServerRepository servers,
    ICloudCredentialRepository credentials,
    ICloudServerFiles files,
    ICloudGovernanceRepository governance,
    ICurrentUserScope scope,
    TimeProvider clock)
{
    public async Task<Result> HandleAsync(Guid serverId, CancellationToken ct)
    {
        var auth = await scope.RequireAsync(serverId, ServerAccessPolicy.CanManageCloud, ct);
        if (!auth.Succeeded)
            return auth;

        await CloudKeyRotation.RevokeAllAsync(credentials, clock, serverId, ct);
        await files.DeleteAsync(serverId, ct);
        if ((await servers.GetByIdAsync(serverId, ct)) is { CloudVaultId: { } vaultId } server)
            await CloudAudit.WriteAsync(governance, vaultId, scope.UserId, "server.revoke_key", $"{server.Name} ({serverId})", ct);
        return Result.Success();
    }
}
