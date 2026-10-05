using Microsoft.Extensions.Logging;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Cloud;

/// <summary>Jogadores de uma nuvem (aba "Jogadores"), com busca por nome ou UUID.</summary>
public sealed class ListCloudPlayers(ICloudAdminRepository repo, ICurrentUserScope scope)
{
    /// <summary>Teto da lista: a tela busca, não pagina milhares de linhas.</summary>
    public const int Limit = 200;

    public async Task<Result<IReadOnlyList<CloudPlayerView>>> HandleAsync(Guid vaultId, string? search,
        CancellationToken ct)
    {
        var access = await CloudVaultAccess.RequireAsync(repo, scope, vaultId, ct);
        if (!access.Succeeded)
            return Result<IReadOnlyList<CloudPlayerView>>.Fail(access.Error!);

        var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        return Result<IReadOnlyList<CloudPlayerView>>.Success(await repo.ListPlayersAsync(vaultId, term, Limit, ct));
    }
}

/// <summary>Os itens de um canal, só leitura.</summary>
public sealed class GetCloudChannelBalances(ICloudAdminRepository repo, ICurrentUserScope scope)
{
    public async Task<Result<IReadOnlyList<CloudBalanceView>>> HandleAsync(Guid vaultId, Guid channelId,
        CancellationToken ct)
    {
        var access = await CloudVaultAccess.RequireAsync(repo, scope, vaultId, ct);
        if (!access.Succeeded)
            return Result<IReadOnlyList<CloudBalanceView>>.Fail(access.Error!);

        // O canal tem de ser DESTA nuvem: senão o id de um canal alheio na URL
        // mostraria os itens de outro dono.
        var channel = await repo.GetChannelAsync(channelId, ct);
        if (channel is null || channel.VaultId != vaultId)
            return Result<IReadOnlyList<CloudBalanceView>>.Fail("Canal não encontrado.");

        return Result<IReadOnlyList<CloudBalanceView>>.Success(await repo.ListChannelBalancesAsync(channelId, ct));
    }
}

/// <summary>
///     Descongela um canal (congelado por um lote em quarentena). Não resolve a
///     quarentena — isso é da tela de quarentena (fatia D): só devolve o canal
///     ao uso depois que o dono olhou o que aconteceu.
/// </summary>
public sealed partial class UnfreezeCloudChannel(
    ICloudAdminRepository repo,
    ICurrentUserScope scope,
    ILogger<UnfreezeCloudChannel> logger)
{
    public async Task<Result> HandleAsync(Guid vaultId, Guid channelId, CancellationToken ct)
    {
        var access = await CloudVaultAccess.RequireAsync(repo, scope, vaultId, ct);
        if (!access.Succeeded)
            return Result.Fail(access.Error!);

        var channel = await repo.GetChannelAsync(channelId, ct);
        if (channel is null || channel.VaultId != vaultId)
            return Result.Fail("Canal não encontrado.");

        channel.Unfreeze();
        await repo.UpdateChannelAsync(channel, ct);
        LogUnfrozen(channelId, scope.UserId);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Nuvem: canal {ChannelId} descongelado (por {UserId}).")]
    private partial void LogUnfrozen(Guid channelId, Guid? userId);
}

/// <summary>
///     Libera à força o lease de um jogador (servidor morto de vez). Lotes que
///     aquele servidor ainda tenha no diário chegarão com época velha e irão para
///     a quarentena — por isso a tela avisa antes.
/// </summary>
public sealed partial class ForceReleaseCloudLease(
    ICloudAdminRepository repo,
    ICloudStorageRepository storage,
    ICurrentUserScope scope,
    ILogger<ForceReleaseCloudLease> logger)
{
    public async Task<Result> HandleAsync(Guid vaultId, string playerUuid, CancellationToken ct)
    {
        var access = await CloudVaultAccess.RequireAsync(repo, scope, vaultId, ct);
        if (!access.Succeeded)
            return Result.Fail(access.Error!);

        var player = CloudChannel.NormalizePlayerUuid(playerUuid);
        var lease = player is null ? null : await storage.GetLeaseAsync(vaultId, player, ct);
        if (lease?.HolderServerId is null)
            return Result.Fail("Nenhum servidor está com os canais deste jogador.");

        var read = lease.Version;
        lease.ForceRelease();
        if (!await storage.SaveLeaseAsync(lease, read, ct))
            return Result.Fail("O lease mudou agora mesmo; atualize a tela e tente de novo.");

        LogForced(vaultId, player!, scope.UserId);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Nuvem: lease de {Player} na nuvem {VaultId} liberado à força (por {UserId}).")]
    private partial void LogForced(Guid vaultId, string player, Guid? userId);
}
