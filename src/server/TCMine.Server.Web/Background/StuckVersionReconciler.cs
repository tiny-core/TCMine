using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Web.Background;

/// <summary>
///     No arranque, desencalha versões que ficaram em Resolving.
///     As filas de ingestão e importação vivem em memória: se o processo cai (ou
///     é reiniciado em deploy) no meio de um pack, o job morre junto mas a coluna
///     continua dizendo "resolvendo". A tela então mente para sempre, e nem o
///     reparo funciona — ele só aceita versão que falhou. Aqui a versão volta a
///     um estado honesto e o admin decide continuar.
/// </summary>
public sealed partial class StuckVersionReconciler(
    IServiceScopeFactory scopeFactory,
    ILogger<StuckVersionReconciler> logger) : BackgroundService
{
    private readonly ILogger<StuckVersionReconciler> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IModpackRepository>();

            var stuck = await repository.ListStuckResolvingAsync(stoppingToken);
            foreach (var version in stuck)
            {
                version.MarkFailed(
                    "A resolução foi interrompida quando o servidor reiniciou. "
                    + "Use 'Tentar novamente' — o que já foi baixado será mantido.");

                await repository.UpdateVersionAsync(version, stoppingToken);
                LogRecovered(version.Id);
            }
        }
        catch (Exception ex)
        {
            // Reconciliação é conveniência: falhar aqui não pode impedir a
            // aplicação de subir.
            LogFailed(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Versão {VersionId} estava presa em Resolving; marcada para reparo.")]
    private partial void LogRecovered(Guid versionId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao reconciliar versões presas em Resolving.")]
    private partial void LogFailed(Exception ex);
}
