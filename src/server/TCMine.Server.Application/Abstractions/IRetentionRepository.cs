namespace TCMine.Server.Application.Abstractions;

/// <summary>Prazo do histórico de jogo de uma nuvem (0 = para sempre).</summary>
public sealed record CloudVaultRetention(Guid VaultId, int GameHistoryDays);

/// <summary>
///     Um servidor com histórico de jogo antigo numa nuvem, com o que decide se
///     (e até onde) ele pode ser apagado. <paramref name="OldestBackupAt" />:
///     o backup de mundo mais antigo que ainda existe dele (nulo = nenhum, ou o
///     servidor já foi apagado).
/// </summary>
public sealed record CloudServerHistory(Guid ServerId, bool HasOpenIncident, DateTimeOffset? OldestBackupAt);

public sealed record GameHistoryPurged(int LedgerRows, int Batches);

public sealed record HousekeepingPurged(int Invites, int AccessRequests);

/// <summary>
///     Apaga o que venceu o prazo. As REGRAS (prazo, proteções) ficam no caso de
///     uso <c>RunRetention</c>; aqui só a consulta e o apagar em blocos, cada
///     bloco numa ida curta ao banco para não segurar a tabela.
/// </summary>
public interface IRetentionRepository
{
    Task<IReadOnlyList<CloudVaultRetention>> ListVaultRetentionAsync(CancellationToken ct);

    /// <summary>Servidores com lotes desta nuvem criados antes de <paramref name="before" />.</summary>
    Task<IReadOnlyList<CloudServerHistory>> ListServersWithGameHistoryAsync(Guid vaultId, DateTimeOffset before,
        CancellationToken ct);

    /// <summary>
    ///     Apaga, deste servidor nesta nuvem e criado antes de <paramref name="before" />:
    ///     as linhas de ledger de origem JOGO e, depois, os lotes aplicados ou
    ///     descartados que nenhuma linha de ledger nem quarentena referencia mais.
    ///     Linhas de decisão do dono e seus lotes ficam, sempre.
    /// </summary>
    [NotVisibleToLauncher("histórico da nuvem: o launcher não lê a nuvem")]
    Task<GameHistoryPurged> PurgeServerGameHistoryAsync(Guid vaultId, Guid serverId, DateTimeOffset before,
        CancellationToken ct);

    [NotVisibleToLauncher("o feed de atividade é só do painel")]
    Task<int> PurgeActivityAsync(DateTimeOffset before, CancellationToken ct);

    /// <summary>
    ///     Convites que já não valem (usados, revogados ou expirados) e pedidos de
    ///     acesso resolvidos, criados antes de <paramref name="before" />. O
    ///     pedido negado só sai depois de resolvido há tanto tempo quanto o prazo.
    /// </summary>
    [NotVisibleToLauncher("o launcher só mostra acesso concedido ou pedido pendente, e esses nunca saem")]
    Task<HousekeepingPurged> PurgeHousekeepingAsync(DateTimeOffset before, DateTimeOffset now, CancellationToken ct);
}
