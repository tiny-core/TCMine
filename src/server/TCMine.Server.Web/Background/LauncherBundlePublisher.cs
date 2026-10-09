using Microsoft.Extensions.Options;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Web.Configuration;

namespace TCMine.Server.Web.Background;

/// <summary>
///     Publica, no arranque, o launcher que veio na imagem — com o endereço
///     público desta instalação embutido. Em segundo plano: empacotar leva de
///     15 a 30 segundos na primeira vez, e o painel não pode esperar por isso.
///     Até terminar, a página pública só diz que o launcher ainda não foi
///     publicado; nos arranques seguintes, sem mudança, não há o que fazer.
/// </summary>
public sealed partial class LauncherBundlePublisher(
    ILauncherBundle bundle,
    IOptions<ServerOptions> server,
    ILogger<LauncherBundlePublisher> logger) : BackgroundService
{
    private readonly ILogger<LauncherBundlePublisher> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // "Congelar" é o admin dizendo que o feed não muda agora (ver
        // ServerOptions): publicar por cima seria desobedecê-lo.
        if (server.Value.FreezeLauncherUpdates)
        {
            LogCongelado();
            return;
        }

        try
        {
            var outcome = await bundle.PublishAsync(server.Value.PublicUrl, stoppingToken);
            LogResultado(outcome);

            if (server.Value.PublicUrl is null && outcome is LauncherBundleOutcome.Published)
                LogSemEndereco();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Falhar aqui não derruba nada: o feed fica como estava, e quem já
            // tem o launcher continua jogando.
            LogFalha(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Launcher embutido: {Outcome}.")]
    private partial void LogResultado(LauncherBundleOutcome outcome);

    [LoggerMessage(Level = LogLevel.Warning,
        Message =
            "Launcher publicado SEM endereço embutido: defina Server:PublicUrl para o jogador não precisar digitá-lo.")]
    private partial void LogSemEndereco();

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Server:FreezeLauncherUpdates ligado: o launcher embutido não é publicado.")]
    private partial void LogCongelado();

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao publicar o launcher embutido; o feed fica como estava.")]
    private partial void LogFalha(Exception ex);
}
