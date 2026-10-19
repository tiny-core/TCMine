using Microsoft.Extensions.Logging;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Application.Storage;

/// <summary>Quanto cada rodada apagou, para o log e para os testes.</summary>
public sealed record RetentionReport(
    int LedgerRows,
    int Batches,
    int ServersProtected,
    int ActivityEvents,
    int Invites,
    int AccessRequests);

/// <summary>
///     A rodada diária de retenção (fase 5.4). A regra: apaga o que cresce com o
///     JOGO e guarda para sempre o que vem de DECISÃO do dono — ajustes, estornos,
///     quarentenas, incidentes e a auditoria são poucas linhas e são a trilha de
///     quem mexeu nos saldos de quem.
///     O que expira: o histórico de jogo de cada nuvem (ledger de origem jogo +
///     lotes, prazo da nuvem), o feed de atividade (prazo da instalação) e a
///     limpeza doméstica (convites mortos e pedidos resolvidos, prazo fixo).
///     Proteções do histórico de jogo, que valem com o prazo vencido:
///     - servidor com incidente de rollback ABERTO não perde nada: a prévia do
///       estorno ainda vai ler os lotes posteriores ao checkpoint;
///     - nada mais novo que o backup de mundo mais antigo do servidor: restaurar
///       esse backup abre um incidente, e o estorno precisa dos lotes desde ele.
///       Cópia feita fora do TCMine não entra na conta.
///     Os saldos não dependem de nada disto: são o estado atual, guardado à parte.
/// </summary>
public sealed partial class RunRetention(
    IRetentionRepository repository,
    ISettingsRepository settings,
    TimeProvider clock,
    ILogger<RunRetention> logger)
{
    /// <summary>Convites mortos e pedidos de acesso resolvidos: fixo, não é configuração.</summary>
    public const int HousekeepingDays = 30;

    public async Task<RetentionReport> HandleAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        int ledger = 0, batches = 0, protectedServers = 0;

        foreach (var vault in await repository.ListVaultRetentionAsync(ct))
        {
            if (vault.GameHistoryDays <= 0)
                continue; // para sempre

            var cutoff = now.AddDays(-vault.GameHistoryDays);
            foreach (var server in await repository.ListServersWithGameHistoryAsync(vault.VaultId, cutoff, ct))
            {
                if (CutoffFor(server, cutoff) is not { } serverCutoff)
                {
                    protectedServers++;
                    continue;
                }

                var purged = await repository.PurgeServerGameHistoryAsync(vault.VaultId, server.ServerId,
                    serverCutoff, ct);
                ledger += purged.LedgerRows;
                batches += purged.Batches;
            }
        }

        var activity = 0;
        var config = await settings.GetAsync(ct);
        if (config.ActivityRetentionDays > 0)
            activity = await repository.PurgeActivityAsync(now.AddDays(-config.ActivityRetentionDays), ct);

        var housekeeping = await repository.PurgeHousekeepingAsync(now.AddDays(-HousekeepingDays), now, ct);

        var report = new RetentionReport(ledger, batches, protectedServers, activity, housekeeping.Invites,
            housekeeping.AccessRequests);
        LogReport(report.LedgerRows, report.Batches, report.ServersProtected, report.ActivityEvents, report.Invites,
            report.AccessRequests);
        return report;
    }

    /// <summary>
    ///     Até onde o histórico de jogo deste servidor pode ir: o prazo, puxado
    ///     para trás até o backup mais antigo dele. Nulo = não apagar nada agora
    ///     (incidente aberto).
    /// </summary>
    public static DateTimeOffset? CutoffFor(CloudServerHistory server, DateTimeOffset cutoff)
    {
        if (server.HasOpenIncident)
            return null;
        return server.OldestBackupAt is { } oldest && oldest < cutoff ? oldest : cutoff;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Retenção: {Ledger} linhas de ledger e {Batches} lotes de jogo apagados ({ProtectedServers} servidores "
                  + "protegidos por incidente aberto); {Activity} eventos de atividade; {Invites} convites e "
                  + "{AccessRequests} pedidos de acesso.")]
    private partial void LogReport(int ledger, int batches, int protectedServers, int activity, int invites,
        int accessRequests);
}
