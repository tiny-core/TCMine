using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Abstractions;

/// <summary>Quarentena com o lote que a originou, para a tela mostrar sem join no caso de uso.</summary>
public sealed record CloudQuarantineView(CloudQuarantine Quarantine, CloudBatch Batch);

/// <summary>
///     Linha do histórico de saldos para a aba de auditoria.
///     <paramref name="PlayerName" />: nome da conta do TCMine com esse UUID, se houver.
/// </summary>
public sealed record CloudLedgerView(
    Guid Id,
    DateTimeOffset At,
    string PlayerUuid,
    string? PlayerName,
    string ChannelName,
    string ItemId,
    string DisplayName,
    long Delta,
    long BalanceAfter,
    CloudLedgerSource Source,
    string? Reason);

/// <summary>
///     Filtros do histórico de saldos, todos opcionais e aplicados no banco.
///     <paramref name="PlayerUuid" /> já normalizado (32 hex, minúsculo).
///     <paramref name="Item" />: trecho do id ou do nome do item, sem diferenciar
///     maiúsculas. <paramref name="From" />/<paramref name="Until" />: período
///     [From, Until). <paramref name="Before" />: cursor da página (ver
///     <see cref="CursorPage{T}" />).
/// </summary>
public sealed record CloudLedgerQuery(
    Guid VaultId,
    string? PlayerUuid,
    string? Item,
    CloudLedgerSource? Source,
    DateTimeOffset? From,
    DateTimeOffset? Until,
    Guid? Before,
    int PageSize);

/// <summary>
///     Regras de item, suspeitos, incidentes de rollback, operações em dúvida,
///     quarentena e auditoria da nuvem — tudo o que o dono revisa e decide.
/// </summary>
public interface ICloudGovernanceRepository
{
    // Regras
    Task<IReadOnlyList<CloudItemRule>> ListRulesAsync(Guid vaultId, CancellationToken ct);
    Task<CloudItemRule?> GetRuleAsync(Guid id, CancellationToken ct);

    /// <summary>False se já existe regra para o mesmo (escopo, padrão).</summary>
    Task<bool> AddRuleAsync(CloudItemRule rule, CancellationToken ct);

    Task RemoveRuleAsync(Guid id, CancellationToken ct);

    // Suspeitos
    /// <summary>Soma as tentativas por item (cria a linha na primeira vez).</summary>
    Task RecordSuspectsAsync(Guid vaultId, IReadOnlyList<(string ItemId, string Evidence, long Attempts)> items,
        DateTimeOffset now, CancellationToken ct);

    Task<IReadOnlyList<CloudSuspectItem>> ListSuspectsAsync(Guid vaultId, CloudSuspectStatus? status,
        CancellationToken ct);

    Task<CloudSuspectItem?> GetSuspectAsync(Guid id, CancellationToken ct);
    Task UpdateSuspectAsync(CloudSuspectItem suspect, CancellationToken ct);

    // Operações em dúvida
    /// <summary>Grava as que ainda não existem (mesmo ReportId/Index = reenvio, ignorado).</summary>
    Task AddDoubtfulAsync(IReadOnlyList<CloudDoubtfulOperation> operations, CancellationToken ct);

    Task<IReadOnlyList<CloudDoubtfulOperation>> ListDoubtfulAsync(Guid vaultId, bool openOnly, CancellationToken ct);
    Task<CloudDoubtfulOperation?> GetDoubtfulAsync(Guid id, CancellationToken ct);
    Task UpdateDoubtfulAsync(CloudDoubtfulOperation operation, CancellationToken ct);

    // Incidentes de rollback
    Task<CloudRollbackIncident?> GetOpenIncidentAsync(Guid serverId, CancellationToken ct);
    Task AddIncidentAsync(CloudRollbackIncident incident, CancellationToken ct);
    Task<IReadOnlyList<CloudRollbackIncident>> ListIncidentsAsync(Guid vaultId, bool openOnly, CancellationToken ct);
    Task<CloudRollbackIncident?> GetIncidentAsync(Guid id, CancellationToken ct);

    /// <summary>
    ///     Até onde o mundo deste servidor está confirmado, por jogador: as marcas
    ///     d'água (<c>CloudWatermark</c>). Uma linha por jogador, sem ler lotes.
    /// </summary>
    Task<IReadOnlyDictionary<string, (long Epoch, long Seq)>> WorldWatermarksAsync(Guid vaultId, Guid serverId,
        CancellationToken ct);

    // Quarentena
    Task<IReadOnlyList<CloudQuarantineView>> ListQuarantineAsync(Guid vaultId, bool openOnly, CancellationToken ct);
    Task<CloudQuarantineView?> GetQuarantineAsync(Guid id, CancellationToken ct);
    Task UpdateQuarantineAsync(CloudQuarantine quarantine, CloudBatch batch, CancellationToken ct);

    /// <summary>Id do item e nome, para telas que só têm o id do tipo de item.</summary>
    Task<IReadOnlyDictionary<Guid, (string ItemId, string DisplayName)>> ItemNamesAsync(IReadOnlyCollection<Guid> ids,
        CancellationToken ct);

    // Auditoria
    Task AddAuditAsync(CloudAdminAuditEntry entry, CancellationToken ct);

    /// <summary>Decisões do painel nesta nuvem, da mais nova para a mais velha.</summary>
    Task<CursorPage<CloudAdminAuditEntry>> ListAuditAsync(Guid vaultId, Guid? before, int pageSize,
        CancellationToken ct);

    /// <summary>Movimentos de itens desta nuvem, do mais novo para o mais velho.</summary>
    Task<CursorPage<CloudLedgerView>> ListLedgerAsync(CloudLedgerQuery query, CancellationToken ct);
}
