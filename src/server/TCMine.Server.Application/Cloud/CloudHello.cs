using Microsoft.Extensions.Logging;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Application.Cloud;

/// <summary>
///     Boot do servidor de jogo: registra o contato (versão do mod, mundo) e
///     devolve a configuração da nuvem. Sem hello concluído o mod não pede canal
///     nenhum.
///     A detecção de "mundo que voltou no tempo" pelo checkpoint entra com os
///     incidentes de rollback (fatia do painel); por ora o checkpoint é recebido
///     e só registrado.
/// </summary>
public sealed partial class CloudHello(
    ICloudCredentialRepository credentials,
    TimeProvider clock,
    ILogger<CloudHello> logger)
{
    public async Task<CloudCallResult<CloudHelloReply>> HandleAsync(CloudServerContext ctx, CloudHelloRequest request,
        CancellationToken ct)
    {
        var vault = await credentials.GetVaultAsync(ctx.VaultId, ct);
        if (vault is null)
            return CloudCallResult<CloudHelloReply>.Forbidden("Nuvem não encontrada.");

        var credential = await credentials.FindByIdAsync(ctx.CredentialId, ct);
        if (credential is not null)
        {
            credential.RecordContact(clock.GetUtcNow(), request.ModVersion, request.Checkpoint.WorldId);
            await credentials.UpdateAsync(credential, ct);
        }

        LogHello(ctx.ServerId, request.ModVersion ?? "?", request.Checkpoint.WorldId,
            request.Checkpoint.Players?.Count ?? 0);

        return CloudCallResult<CloudHelloReply>.Ok(new CloudHelloReply(
            CloudProtocol.Current,
            vault.PolicyMode.ToString(),
            vault.PolicyVersion,
            new CloudQuotaDto(vault.MaxTypesPerChannel, vault.MaxTotalPerChannel),
            vault.MaxItemBytes,
            vault.MaxChannelsPerPlayer,
            vault.LeaseTtlMinutes * 60,
            ReadOnly: !vault.IsEnabled,
            ReadOnlyReason: vault.IsEnabled ? null : "Nuvem desligada pelo dono."));
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Nuvem: hello do servidor {ServerId} (mod {ModVersion}, mundo {WorldId}, {Players} jogadores no checkpoint).")]
    private partial void LogHello(Guid serverId, string modVersion, Guid worldId, int players);
}
