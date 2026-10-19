using TCMine.Server.Application.Cloud;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Application.Servers;

namespace TCMine.Server.Web.Background;

/// <summary>
///     No arranque, retoma o trabalho que o processo anterior deixou pela metade.
///     As duas filas vivem em memória, então um deploy no meio de um pack mata o
///     job — mas não o pedido, que ficou gravado. A decisão de retomar, desistir
///     ou devolver ao rascunho é regra de negócio e mora nos casos de uso; aqui
///     fica só o gatilho do arranque e a garantia de que uma falha nele não
///     impede a aplicação de subir.
/// </summary>
public sealed partial class InterruptedWorkRecovery(
    IServiceScopeFactory scopeFactory,
    ILogger<InterruptedWorkRecovery> logger) : BackgroundService
{
    private readonly ILogger<InterruptedWorkRecovery> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        // Ingestões primeiro: uma importação retomada enfileira ingestão por
        // conta própria, e é melhor que isso aconteça depois de a recuperação de
        // ingestões já ter passado — senão o mesmo trabalho entraria duas vezes.
        await RunAsync(
            () => scope.ServiceProvider.GetRequiredService<RecoverInterruptedIngestions>().HandleAsync(stoppingToken),
            LogIngestionsRecovered, LogIngestionsFailed);

        await RunAsync(
            () => scope.ServiceProvider.GetRequiredService<RecoverInterruptedImports>().HandleAsync(stoppingToken),
            LogImportsRecovered, LogImportsFailed);

        // Leases da nuvem: enquanto o processo esteve fora, nenhum servidor de
        // jogo conseguiu renovar. Sem isto eles expirariam por culpa nossa.
        await RunAsync(
            () => scope.ServiceProvider.GetRequiredService<ExtendCloudLeasesAfterOutage>().HandleAsync(stoppingToken),
            LogCloudLeasesExtended, LogCloudLeasesFailed);

        // Local e barato (um arquivo por servidor), e é proteção, não
        // conveniência: sem a marca, trocar a versão de um servidor parado com
        // mundo não faz backup. Por isso vem antes dos outros preenchimentos.
        await RunAsync(
            () => scope.ServiceProvider.GetRequiredService<BackfillWorldInitialized>().HandleAsync(stoppingToken),
            LogWorldsMarked, LogWorldsFailed);

        // Local e barato (lê jars que já estão no disco): antes do server pack,
        // que depende da rede.
        await RunAsync(
            () => scope.ServiceProvider.GetRequiredService<BackfillManualModIdentity>().HandleAsync(stoppingToken),
            LogManualModsIdentified, LogManualModsFailed);

        // Também local: só lê o banco. Antes do server pack, porque é a ligação
        // com o autor que diz de qual release buscar o server pack.
        await RunAsync(
            () => scope.ServiceProvider.GetRequiredService<BackfillUpstreamLineage>().HandleAsync(stoppingToken),
            LogLineageRestored, LogLineageFailed);

        // Por último, e de propósito: descobrir o server pack de packs já
        // importados é conveniência pura, faz chamadas à origem e não tem pressa
        // nenhuma. O que ficar para trás (origem fora do ar, sem chave de API)
        // é tentado no próximo arranque, porque a condição se mantém.
        await RunAsync(
            () => scope.ServiceProvider.GetRequiredService<BackfillServerPacks>().HandleAsync(stoppingToken),
            LogServerPacksBackfilled, LogServerPacksFailed);
    }

    /// <summary>
    ///     Recuperação é conveniência: falhar aqui não pode impedir a aplicação de
    ///     subir, e a falha de uma das duas não pode cancelar a outra.
    /// </summary>
    private static async Task RunAsync(
        Func<Task<int>> acao, Action<int> aoRecuperar, Action<Exception> aoFalhar)
    {
        try
        {
            var total = await acao();
            if (total > 0)
                aoRecuperar(total);
        }
        catch (Exception ex)
        {
            aoFalhar(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{Count} ingestão(ões) interrompida(s) voltaram para a fila.")]
    private partial void LogIngestionsRecovered(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao retomar as ingestões interrompidas.")]
    private partial void LogIngestionsFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{Count} importação(ões) interrompida(s) voltaram para a fila.")]
    private partial void LogImportsRecovered(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao retomar as importações interrompidas.")]
    private partial void LogImportsFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Nuvem: {Count} lease(s) estendido(s) após o arranque.")]
    private partial void LogCloudLeasesExtended(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao estender os leases da nuvem no arranque.")]
    private partial void LogCloudLeasesFailed(Exception ex);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "{Count} versão(ões) já importada(s) passaram a conhecer o server pack do autor.")]
    private partial void LogServerPacksBackfilled(int count);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{Count} servidor(es) com mundo no disco ganharam a marca: a troca de versão passa a fazer backup.")]
    private partial void LogWorldsMarked(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao marcar os servidores que já têm mundo.")]
    private partial void LogWorldsFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{Count} mod(s) enviado(s) à mão ganharam identidade e passam a aparecer no inventário.")]
    private partial void LogManualModsIdentified(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao dar identidade aos mods enviados à mão.")]
    private partial void LogManualModsFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{Count} versão(ões) recuperaram a ligação com o pack do autor.")]
    private partial void LogLineageRestored(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao recuperar a ligação das versões com o pack do autor.")]
    private partial void LogLineageFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao descobrir os server packs das versões já importadas.")]
    private partial void LogServerPacksFailed(Exception ex);
}
