using TCMine.Server.Application.Modpacks;

namespace TCMine.Server.Web.Background;

/// <summary>
///     Consome a fila de importação, um pack por vez.
///     Serial de propósito: importar dois packs grandes ao mesmo tempo brigaria
///     por banda e por escrita no SQLite sem terminar nenhum mais rápido.
/// </summary>
public sealed partial class ImportWorker(
    ImportQueue queue,
    JobProgressRegistry progress,
    IServiceScopeFactory scopeFactory,
    ILogger<ImportWorker> logger) : BackgroundService
{
    private readonly ILogger<ImportWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var useCase = scope.ServiceProvider.GetRequiredService<ImportUpstreamPack>();

                var result = await useCase.HandleAsync(
                    job.Origin, job.ProjectId, job.FileId, stoppingToken, job.Id, job.DisplayName);

                if (!result.Succeeded)
                {
                    progress.Complete(job.Id, result.Error);
                    LogImportRefused(job.ProjectId, result.Error!);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Uma importação com erro não pode derrubar o worker: a fila
                // travaria para todas as próximas. E o acompanhamento precisa
                // encerrar, senão a barra fica girando para sempre.
                progress.Complete(job.Id, ex.Message);
                LogImportFailed(ex, job.ProjectId);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Importação do pack '{ProjectId}' recusada: {Reason}")]
    private partial void LogImportRefused(string projectId, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao importar o pack '{ProjectId}'.")]
    private partial void LogImportFailed(Exception ex, string projectId);
}
