using Microsoft.Extensions.Logging;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Application.Cloud;

/// <summary>
///     Antes de cada start de um servidor gerenciado: se ele está ligado a uma
///     nuvem, gera uma chave nova (revogando a anterior) e grava o arquivo que o
///     mod lê; se não está, apaga o arquivo. Ninguém vê a chave — ela vai direto
///     do TCMine para a pasta do servidor.
///     Chave nova a cada start em vez de reaproveitar: o banco só tem o hash,
///     então reaproveitar exigiria guardar a chave em claro em algum lugar.
///     Falha aqui não impede o servidor de subir (quem chama trata): sem
///     arquivo, o mod simplesmente fica com a nuvem desligada.
/// </summary>
public sealed partial class ProvisionServerCloudKey(
    IServerRepository servers,
    ICloudCredentialRepository credentials,
    ICloudServerFiles files,
    ICloudEndpointSource endpoint,
    TimeProvider clock,
    ILogger<ProvisionServerCloudKey> logger)
{
    public async Task HandleAsync(Guid serverId, CancellationToken ct)
    {
        var server = await servers.GetByIdAsync(serverId, ct);
        if (server?.CloudVaultId is not { } vaultId)
        {
            await files.DeleteAsync(serverId, ct);
            return;
        }

        if (endpoint.ServerFacingUrl is not { } url)
        {
            LogNoUrl(serverId);
            await files.DeleteAsync(serverId, ct);
            return;
        }

        var key = await CloudKeyRotation.RotateAsync(credentials, clock, serverId, vaultId, ct);
        await files.WriteAsync(serverId, url, key, ct);
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message =
            "Nuvem: servidor {ServerId} está ligado a uma nuvem, mas o TCMine não tem Server:PublicUrl nem Server:CloudUrl; a nuvem fica desligada nele.")]
    private partial void LogNoUrl(Guid serverId);
}
