using Microsoft.Extensions.Logging;
using TCMine.Launcher.Core.Abstractions;
using Velopack;

namespace TCMine.Launcher.Infrastructure.Updates;

/// <summary>
///     Atualiza o launcher pelo Velopack, contra o feed do servidor.
///     Executado a partir do código-fonte, o <c>IsInstalled</c> é falso e não há
///     o que substituir — sai em silêncio, que é o comportamento certo: quem
///     desenvolve não quer o launcher a reiniciar-se a meio de uma depuração.
///     Falhar a atualizar NUNCA impede usar o launcher. Uma rede em baixo, um
///     feed vazio ou um servidor a devolver lixo são todos o mesmo desfecho aqui:
///     continua-se com a versão que já está instalada.
/// </summary>
public sealed partial class VelopackLauncherUpdater(
    ILogger<VelopackLauncherUpdater> logger) : ILauncherUpdater
{
    private readonly ILogger<VelopackLauncherUpdater> _logger = logger;

    public async Task<bool> UpdateAsync(Uri feedUrl, CancellationToken ct)
    {
        try
        {
            var manager = new UpdateManager(feedUrl.ToString());

            if (!manager.IsInstalled)
                return false;

            var novidade = await manager.CheckForUpdatesAsync();

            if (novidade is null)
                return false;

            // CA1873 avisa que o argumento pode ser caro de avaliar com o log
            // desligado, e aqui não é: um ToString de versão, uma vez por
            // atualização encontrada — que é raro por definição. Passar o objeto
            // em vez da string não serve: o gerador recusa um parâmetro que não
            // prometa ToString.
#pragma warning disable CA1873
            LogEncontrou(novidade.TargetFullRelease.Version.ToString());
#pragma warning restore CA1873

            await manager.DownloadUpdatesAsync(novidade, cancelToken: ct);

            // Reinicia AQUI e não no fecho: a substituição acontece com o
            // processo fora do caminho, e adiar significaria manter uma cópia
            // pronta no disco à espera de um fecho que pode nunca ser limpo.
            manager.ApplyUpdatesAndRestart(novidade);

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFalhou(ex);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Atualização do launcher encontrada: {Versao}.")]
    private partial void LogEncontrou(string versao);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Não foi possível atualizar o launcher; seguindo com a versão atual.")]
    private partial void LogFalhou(Exception ex);
}
