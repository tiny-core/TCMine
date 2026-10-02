using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Abstractions;

/// <summary>Saldo de um item num canal, como o acquire entrega ao servidor de jogo.</summary>
public sealed record CloudSnapshotItem(Guid ChannelId, CloudItemType ItemType, long Amount);

/// <summary>Uma variação a gravar: o item já tem Id (novo ou existente).</summary>
public sealed record CloudBalanceChange(Guid ChannelId, Guid ItemTypeId, long Delta, long BalanceAfter);

/// <summary>
///     Persistência da nuvem do lado do protocolo (lease, lotes, saldos).
///     Os métodos de gravação devolvem <c>false</c> quando outra requisição
///     mexeu no mesmo lease no meio do caminho (concorrência otimista): quem
///     chama responde "conflito" e o servidor de jogo tenta de novo. Não é erro.
/// </summary>
public interface ICloudStorageRepository
{
    Task<CloudLease?> GetLeaseAsync(Guid vaultId, string playerUuid, CancellationToken ct);

    /// <summary>
    ///     Grava o lease. <paramref name="expectedVersion" /> é a versão LIDA
    ///     (antes das mudanças); nulo = lease novo. False em conflito.
    /// </summary>
    Task<bool> SaveLeaseAsync(CloudLease lease, long? expectedVersion, CancellationToken ct);

    /// <summary>Leases segurados por alguém, com o TTL da nuvem de cada um (extensão após queda do TCMine).</summary>
    Task<IReadOnlyList<(CloudLease Lease, TimeSpan Ttl)>> ListHeldLeasesAsync(CancellationToken ct);

    Task<IReadOnlyList<CloudChannel>> ListChannelsAsync(Guid vaultId, string playerUuid, CancellationToken ct);

    Task AddChannelAsync(CloudChannel channel, CancellationToken ct);

    /// <summary>Saldos maiores que zero dos canais, com o tipo de item.</summary>
    Task<IReadOnlyList<CloudSnapshotItem>> LoadSnapshotAsync(IReadOnlyCollection<Guid> channelIds, CancellationToken ct);

    /// <summary>Todos os saldos (inclusive zero) dos canais: base para validar um lote e as cotas.</summary>
    Task<IReadOnlyList<CloudBalance>> ListBalancesAsync(IReadOnlyCollection<Guid> channelIds, CancellationToken ct);

    /// <summary>Impressão digital → id, dos itens que o banco já conhece (sem os bytes).</summary>
    Task<IReadOnlyDictionary<string, Guid>> ItemIdsByFingerprintAsync(IReadOnlyCollection<string> fingerprints,
        CancellationToken ct);

    /// <summary>Só id → impressão digital (sem os bytes do item, que podem ter KBs cada).</summary>
    Task<IReadOnlyDictionary<Guid, string>> FingerprintsByIdAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    Task<CloudBatch?> FindBatchAsync(Guid vaultId, string playerUuid, long epoch, long seq, CancellationToken ct);

    /// <summary>
    ///     Aplica um lote numa transação só: itens novos, saldos, ledger, o
    ///     registro do lote e o avanço do lease. False em conflito de
    ///     concorrência (nada foi gravado).
    /// </summary>
    Task<bool> CommitAppliedAsync(CloudLease lease, long expectedLeaseVersion, CloudBatch batch,
        IReadOnlyList<CloudItemType> newItemTypes, IReadOnlyList<CloudBalanceChange> changes, CancellationToken ct);

    /// <summary>
    ///     Guarda o lote recusado e congela os canais que ele tocava. Lança
    ///     <c>CloudConcurrencyException</c> se o mesmo lote foi gravado por outra
    ///     requisição no mesmo instante.
    /// </summary>
    Task CommitQuarantineAsync(CloudBatch batch, CloudQuarantine quarantine, IReadOnlyCollection<Guid> channelsToFreeze,
        CancellationToken ct);
}
