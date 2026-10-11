using TCMine.Server.Application.Dns;

namespace TCMine.Server.Web.Background;

/// <summary>
///     Mantém o DNS dos servidores certo sem ninguém clicar.
///     Sincroniza em três ocasiões: pouco depois de arrancar, logo após um pedido
///     (servidor criado, editado ou apagado; configurações salvas) e de tempos em
///     tempos. A rodada periódica é o que acompanha a troca de IP de uma ligação
///     residencial, e é também a nova tentativa de tudo o que falhou antes — a
///     sincronização recalcula a diferença inteira a cada vez.
///     Com o DNS desligado cada rodada é uma leitura das configurações e mais
///     nada: nenhuma chamada sai para a Cloudflare.
/// </summary>
public sealed partial class DnsSyncWorker(
    DnsSyncState state,
    IServiceScopeFactory scopeFactory,
    ILogger<DnsSyncWorker> logger) : BackgroundService
{
    /// <summary>
    ///     Cinco minutos: com o cache de dez minutos do IP público, uma troca de IP
    ///     chega ao DNS em até quinze. Cada rodada custa uma listagem na
    ///     Cloudflare, muito abaixo do limite da API.
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    /// <summary>Deixa o resto do arranque (migrations, Docker) passar à frente.</summary>
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(20);

    /// <summary>
    ///     Espera depois de um pedido: quem edita três servidores em seguida gera
    ///     três pedidos em segundos, e uma sincronização no fim atende todos.
    /// </summary>
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(3);

    private readonly ILogger<DnsSyncWorker> _logger = logger;

    /// <summary>Os problemas da rodada anterior, para o log não repetir o mesmo aviso de cinco em cinco minutos.</summary>
    private string _lastProblems = "";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                // Antes de sincronizar: um pedido que chegue DURANTE a rodada
                // tem de sobreviver a ela e disparar a seguinte.
                state.Drain();

                await SyncOnceAsync(stoppingToken);

                if (await state.WaitAsync(Interval, stoppingToken))
                    await Task.Delay(Debounce, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Desligando: a próxima subida sincroniza.
        }
    }

    private async Task SyncOnceAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            var report = await scope.ServiceProvider.GetRequiredService<SyncGameDns>().HandleAsync(ct);

            state.Record(report);
            LogOutcome(report);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A sincronização devolve os erros da Cloudflare no relatório; o que
            // chega aqui é outra coisa (banco fora do ar, por exemplo). Não pode
            // matar o serviço: a rodada seguinte tenta de novo.
            LogFailed(ex);
        }
    }

    private void LogOutcome(DnsSyncReport report)
    {
        var problems = string.Join(" | ", report.Problems);

        if (report.Created + report.Updated + report.Deleted > 0)
            LogChanged(report.Created, report.Updated, report.Deleted);

        if (problems.Length > 0 && problems != _lastProblems)
            LogProblems(problems);

        _lastProblems = problems;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "DNS dos servidores sincronizado: {Created} criado(s), {Updated} atualizado(s), {Deleted} apagado(s).")]
    private partial void LogChanged(int created, int updated, int deleted);

    [LoggerMessage(Level = LogLevel.Warning, Message = "DNS dos servidores com problemas: {Problems}")]
    private partial void LogProblems(string problems);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao sincronizar o DNS dos servidores.")]
    private partial void LogFailed(Exception ex);
}
