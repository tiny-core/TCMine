using Microsoft.Extensions.Logging;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Cloud;

/// <summary>
///     Login do jogador num servidor de jogo: tenta segurar os canais dele nesta
///     nuvem (plano §4). Entrega a época nova e o snapshot dos canais, ou diz
///     quem está com eles. O primeiro acesso cria o canal padrão.
/// </summary>
public sealed partial class AcquireCloudLease(
    ICloudStorageRepository storage,
    ICloudCredentialRepository credentials,
    IServerRepository servers,
    TimeProvider clock,
    ILogger<AcquireCloudLease> logger)
{
    public async Task<CloudCallResult<CloudAcquireReply>> HandleAsync(CloudServerContext ctx,
        CloudAcquireRequest request, CancellationToken ct)
    {
        var player = CloudChannel.NormalizePlayerUuid(request.PlayerUuid);
        if (player is null)
            return CloudCallResult<CloudAcquireReply>.Invalid("UUID do jogador inválido.");

        var vault = await credentials.GetVaultAsync(ctx.VaultId, ct);
        if (vault is null)
            return CloudCallResult<CloudAcquireReply>.Forbidden("Nuvem não encontrada.");

        var now = clock.GetUtcNow();
        var lease = await storage.GetLeaseAsync(ctx.VaultId, player, ct);
        var readVersion = lease?.Version;
        lease ??= new CloudLease { VaultId = ctx.VaultId, PlayerUuid = player };

        if (!lease.TryAcquire(ctx.ServerId, now, vault.LeaseTtl))
        {
            var holder = lease.HolderServerId is { } id ? (await servers.GetByIdAsync(id, ct))?.Name : null;
            return CloudCallResult<CloudAcquireReply>.Ok(
                new CloudAcquireReply("busy", 0, true, [], holder ?? "outro servidor"));
        }

        if (!await storage.SaveLeaseAsync(lease, readVersion, ct))
            return CloudCallResult<CloudAcquireReply>.Conflict("Outro servidor pediu o mesmo canal agora.");

        var channels = await storage.ListChannelsAsync(ctx.VaultId, player, ct);
        if (channels.Count == 0)
        {
            var created = new CloudChannel
            {
                VaultId = ctx.VaultId, PlayerUuid = player, Name = CloudChannel.DefaultName
            };
            await storage.AddChannelAsync(created, ct);
            channels = [created];
        }

        var snapshot = await storage.LoadSnapshotAsync(channels.Select(c => c.Id).ToArray(), ct);
        var dtos = channels
            .Select(c => new CloudChannelDto(c.Id, c.Name, c.IsFrozen, snapshot
                .Where(s => s.ChannelId == c.Id)
                .Select(s => new CloudChannelItemDto(s.ItemType.Fingerprint, s.Amount, s.ItemType.ItemId,
                    s.ItemType.DisplayName, Convert.ToBase64String(s.ItemType.Encoded)))
                .ToArray()))
            .ToArray();

        LogGranted(player, ctx.ServerId, lease.Epoch);
        var readOnly = !vault.IsEnabled || channels.Any(c => c.IsFrozen);
        return CloudCallResult<CloudAcquireReply>.Ok(
            new CloudAcquireReply("granted", lease.Epoch, readOnly, dtos, null));
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Nuvem: lease de {Player} para o servidor {ServerId}, época {Epoch}.")]
    private partial void LogGranted(string player, Guid serverId, long epoch);
}
