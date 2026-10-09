using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Abstractions;

/// <summary>Uma nuvem na lista do painel, com os totais que a tela mostra.</summary>
public sealed record CloudVaultSummary(Guid Id, string Name, Guid OwnerId, bool IsEnabled, int Servers, int Players);

/// <summary>Chave ativa de um servidor: só o que não é segredo.</summary>
public sealed record CloudServerKeyView(
    Guid ServerId,
    string Prefix,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastSeenAt,
    string? ModVersion);

public sealed record CloudChannelView(
    Guid Id,
    string Name,
    bool Frozen,
    string? FrozenReason,
    int ItemTypes,
    long TotalItems);

/// <summary>Um jogador da nuvem: canais, totais e quem segura o lease agora.</summary>
public sealed record CloudPlayerView(
    string PlayerUuid,
    string? DisplayName,
    IReadOnlyList<CloudChannelView> Channels,
    Guid? HolderServerId,
    long Epoch,
    DateTimeOffset? LeaseExpiresAt);

public sealed record CloudBalanceView(string Fingerprint, string ItemId, string ModId, string DisplayName, long Amount);

/// <summary>Consultas e gravações do painel da nuvem (o protocolo do mod fica no ICloudStorageRepository).</summary>
public interface ICloudAdminRepository
{
    Task AddVaultAsync(CloudVault vault, CancellationToken ct);

    Task UpdateVaultAsync(CloudVault vault, CancellationToken ct);

    Task<CloudVault?> GetVaultAsync(Guid id, CancellationToken ct);

    /// <summary>Nuvens com os totais; <paramref name="ownerId" /> nulo = todas (admin da instalação).</summary>
    Task<IReadOnlyList<CloudVaultSummary>> ListVaultsAsync(Guid? ownerId, CancellationToken ct);

    Task<IReadOnlyList<CloudServerKeyView>> ListActiveKeysAsync(IReadOnlyCollection<Guid> serverIds,
        CancellationToken ct);

    /// <summary>
    ///     Jogadores da nuvem (agrupados pelos canais), com nome quando o UUID
    ///     bate com uma conta do TCMine. <paramref name="search" /> filtra por
    ///     nome ou UUID.
    /// </summary>
    Task<IReadOnlyList<CloudPlayerView>>
        ListPlayersAsync(Guid vaultId, string? search, int limit, CancellationToken ct);

    Task<IReadOnlyList<CloudBalanceView>> ListChannelBalancesAsync(Guid channelId, CancellationToken ct);

    Task<CloudChannel?> GetChannelAsync(Guid id, CancellationToken ct);

    Task UpdateChannelAsync(CloudChannel channel, CancellationToken ct);
}
