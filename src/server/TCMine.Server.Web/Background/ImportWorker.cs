using TCMine.Server.Application.Abstractions;
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
            // Terminou é terminou, deu certo ou não: o rastro sai. A exceção é o
            // desligamento — ali a linha PRECISA ficar, porque é dela que o
            // próximo arranque descobre que havia trabalho em curso.
            var concluido = false;

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var useCase = scope.ServiceProvider.GetRequiredService<ImportUpstreamPack>();

                var result = await useCase.HandleAsync(
                    job.Origin, job.ProjectId, job.FileId, stoppingToken, job.Id, job.DisplayName);

                concluido = true;

                if (!result.Succeeded)
                {
                    progress.Complete(job.Id, result.Error);
                    LogImportRefused(job.ProjectId, result.Error!);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Desligamento no meio: deixa o rastro para o arranque retomar.
                break;
            }
            catch (Exception ex)
            {
                // Uma importação com erro não pode derrubar o worker: a fila
                // travaria para todas as próximas. E o acompanhamento precisa
                // encerrar, senão a barra fica girando para sempre.
                concluido = true;
                progress.Complete(job.Id, ex.Message);
                LogImportFailed(ex, job.ProjectId);
            }
            finally
            {
                if (concluido)
                    await RemoveRequestAsync(job.Id);
            }
        }
    }

    /// <summary>
    ///     Apaga o rastro fora do CancellationToken do desligamento: se o token já
    ///     disparou, passá-lo adiante cancelaria justamente a limpeza e deixaria
    ///     uma importação concluída parecendo interrompida.
    /// </summary>
    private async Task RemoveRequestAsync(Guid requestId)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var requests = scope.ServiceProvider.GetRequiredService<IImportRequestRepository>();

            await requests.RemoveAsync(requestId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Rastro órfão custa uma retomada desnecessária no próximo arranque;
            // derrubar o worker custaria a fila inteira.
            LogCleanupFailed(ex, requestId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Importação do pack '{ProjectId}' recusada: {Reason}")]
    private partial void LogImportRefused(string projectId, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao importar o pack '{ProjectId}'.")]
    private partial void LogImportFailed(Exception ex, string projectId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao apagar o rastro da importação {RequestId}.")]
    private partial void LogCleanupFailed(Exception ex, Guid requestId);
}
