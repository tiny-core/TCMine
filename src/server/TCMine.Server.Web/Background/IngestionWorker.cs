using TCMine.Server.Application.Modpacks;

namespace TCMine.Server.Web.Background;

/// <summary>
///     Consome a fila de ingestão, um job por vez.
///     BackgroundService roda durante toda a vida da aplicação. Cada job cria seu
///     próprio scope de DI porque o serviço de ingestão e o repositório são
///     scoped — um BackgroundService é singleton e não pode injetá-los direto.
/// </summary>
public sealed partial class IngestionWorker(
    IngestionQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<IngestionWorker> logger) : BackgroundService
{
    private readonly ILogger<IngestionWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                // Um scope por job: o mesmo motivo da factory de DbContext —
                // não arrastar estado entre trabalhos.
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<ModpackIngestionService>();

                await service.IngestAsync(job.VersionId, job.Items, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Desligamento normal da aplicação.
                break;
            }
            catch (Exception ex)
            {
                // Um job com erro não pode derrubar o worker inteiro, senão a
                // fila trava para todos os próximos.
                LogJobFailed(ex, job.VersionId);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao processar ingestão da versão {VersionId}.")]
    private partial void LogJobFailed(Exception ex, Guid versionId);
}
