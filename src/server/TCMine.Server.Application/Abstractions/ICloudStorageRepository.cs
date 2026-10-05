using TCMine.Server.Domain.Cloud;
using TCMine.Server.Domain.Common;

namespace TCMine.Server.Application.Abstractions;

/// <summary>Saldo de um item num canal, como o acquire entrega ao servidor de jogo.</summary>
public sealed record CloudSnapshotItem(Guid ChannelId, CloudItemType ItemType, long Amount);

/// <summary>Uma variação a gravar: o item já tem Id (novo ou existente).</summary>
public sealed record CloudBalanceChange(Guid ChannelId, Guid ItemTypeId, long Delta, long BalanceAfter);

/// <summary>Variação feita pelo painel (quarentena aplicada, devolução, estorno), já com o saldo final.</summary>
public sealed record CloudAdminChange(Guid ChannelId, Guid ItemTypeId, long Delta, long BalanceAfter, Guid? BatchId);

/// <summary>
///     Uma decisão do painel que mexe em saldos, gravada numa transação só.
///     <c>Leases</c>: os leases dos jogadores tocados com a versão LIDA (nula =
///     lease novo); todos ganham versão nova, então um acquire concorrente perde a
///     corrida. <c>AlsoUpdate</c>: o que muda junto (quarentena, lote, incidente...).
/// </summary>
public sealed record CloudAdminCommit(
    IReadOnlyList<(CloudLease Lease, long? ReadVersion)> Leases,
    IReadOnlyList<CloudItemType> NewItemTypes,
    IReadOnlyList<CloudAdminChange> Changes,
    CloudLedgerSource Source,
    Guid ActorUserId,
    string Reason,
    IReadOnlyList<Entity> AlsoUpdate);

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

    /// <summary>Grava uma decisão do painel; false se um lease mudou no meio (nada gravado).</summary>
    Task<bool> CommitAdminAsync(CloudAdminCommit commit, CancellationToken ct);

    /// <summary>Lotes APLICADOS de um servidor nesta nuvem.</summary>
    Task<IReadOnlyList<CloudBatch>> ListAppliedBatchesAsync(Guid vaultId, Guid serverId, CancellationToken ct);

    /// <summary>Linhas do ledger geradas por estes lotes.</summary>
    Task<IReadOnlyList<CloudLedgerEntry>> ListLedgerByBatchesAsync(IReadOnlyCollection<Guid> batchIds, CancellationToken ct);

    /// <summary>
    ///     Guarda o lote recusado e congela os canais que ele tocava. Lança
    ///     <c>CloudConcurrencyException</c> se o mesmo lote foi gravado por outra
    ///     requisição no mesmo instante.
    /// </summary>
    Task CommitQuarantineAsync(CloudBatch batch, CloudQuarantine quarantine, IReadOnlyCollection<Guid> channelsToFreeze,
        CancellationToken ct);
}
