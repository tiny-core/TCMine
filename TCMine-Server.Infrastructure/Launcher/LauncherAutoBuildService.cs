using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TCMine_Application.Contracts;
using TCMine_Server.Infrastructure.Server;

namespace TCMine_Server.Infrastructure.Launcher;

/// <summary>
///     Mantém o feed do launcher <b>alinhado à versão do servidor</b> sem ação manual: no boot (e ao salvar
///     as settings) tenta recompilar o launcher se o feed publicado está atrás do servidor e as settings
///     (URL/AzureId) estão prontas. Pega carona no fluxo de container — subir a imagem nova já recompila o
///     launcher. O build corre em segundo plano (via <see cref="LauncherBuildService" />); a página
///     <c>/admin/releases</c> mostra o progresso. Não suportado sem SDK/vpk/fonte (ex.: dev sem isso) — aí
///     apenas registra o aviso e não faz nada.
/// </summary>
public sealed class LauncherAutoBuildService(
    LauncherBuildService build,
    ServerSettingsService settings,
    ILogger<LauncherAutoBuildService> logger) : IHostedService
{
    private CancellationTokenSource? _cts;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = new CancellationTokenSource();

        // Ao salvar as settings (ex.: 1º deploy configura URL/AzureId), tenta alinhar o launcher
        settings.Changed += OnSettingsChanged;

        // Boot: espera o app assentar e então alinha o launcher ao servidor, se preciso
        _ = RunBootCheckAsync(_cts.Token);
        return Task.CompletedTask;
    }

    private async Task RunBootCheckAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            if (await build.TryStartAutoAsync(ct))
                logger.LogInformation(
                    "Auto-build do launcher iniciado no boot (feed atrás do servidor {Version}).",
                    build.TargetVersion);
        }
        catch (OperationCanceledException)
        {
            // Encerrando — normal
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Auto-build do launcher no boot falhou ao iniciar.");
        }
    }

    private void OnSettingsChanged(ServerSettings updated)
    {
        _ = build.TryStartAutoAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        settings.Changed -= OnSettingsChanged;
        _cts?.Cancel();
        return Task.CompletedTask;
    }
}
