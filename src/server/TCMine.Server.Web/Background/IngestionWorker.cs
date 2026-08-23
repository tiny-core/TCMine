using TCMine.Server.Application.Abstractions;
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
    JobProgressRegistry progress,
    IServiceScopeFactory scopeFactory,
    ILogger<IngestionWorker> logger) : BackgroundService
{
    private readonly ILogger<IngestionWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in queue.ReadAllAsync(stoppingToken))
        {
            // O token do JOB, e não o da aplicação: é o que permite cancelar
            // uma ingestão sem derrubar o worker nem as outras da fila. Fica
            // ligado ao desligamento para um deploy não esperar por ela.
            var jobToken = progress.BeginCancellable(job.VersionId, stoppingToken);

            try
            {
                // Um scope por job: o mesmo motivo da factory de DbContext —
                // não arrastar estado entre trabalhos.
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<ModpackIngestionService>();

                await service.IngestAsync(job.VersionId, job.Items, jobToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Desligamento normal da aplicação.
                break;
            }
            catch (OperationCanceledException)
            {
                // Cancelada pelo admin. A versão fica em "Resolvendo" no banco,
                // e é a recuperação do arranque que a devolveria ao rascunho —
                // esperar por um reinício seria deixá-la travada. Devolvemos
                // agora, mantendo o que já baixou.
                LogCancelled(job.VersionId);
                await ReturnToDraftAsync(job.VersionId, stoppingToken);
                progress.Complete(job.VersionId, "Cancelado.");
            }
            catch (Exception ex)
            {
                // Um job com erro não pode derrubar o worker inteiro, senão a
                // fila trava para todos os próximos.
                LogJobFailed(ex, job.VersionId);

                // Rede de segurança: se o serviço não encerrou o acompanhamento
                // (porque estourou antes), a barra de progresso ficaria girando
                // para sempre e a versão presa em "Resolvendo".
                progress.Complete(job.VersionId, ex.Message);
            }
            finally
            {
                progress.EndCancellable(job.VersionId);
            }
        }
    }

    /// <summary>
    ///     Tira a versão de "Resolvendo" depois de um cancelamento.
    ///     Sem isto ela ficaria presa nesse estado: não dá para editar, não dá
    ///     para publicar, e a única saída seria reiniciar a aplicação para a
    ///     recuperação do arranque a soltar.
    /// </summary>
    private async Task ReturnToDraftAsync(Guid versionId, CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IModpackRepository>();

            var version = await repository.GetVersionAsync(versionId, ct);
            if (version is null)
                return;

            version.ReturnToDraft();
            await repository.SaveVersionStateAsync(version, ct);
        }
        catch (Exception ex)
        {
            // Se nem isto deu, a recuperação do arranque ainda pega — mas o
            // admin precisa saber por que a versão continua em "Resolvendo".
            LogReturnFailed(ex, versionId);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao processar ingestão da versão {VersionId}.")]
    private partial void LogJobFailed(Exception ex, Guid versionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ingestão da versão {VersionId} cancelada pelo admin.")]
    private partial void LogCancelled(Guid versionId);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "A versão {VersionId} ficou em Resolvendo depois do cancelamento.")]
    private partial void LogReturnFailed(Exception ex, Guid versionId);
}
