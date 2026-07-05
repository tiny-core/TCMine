using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TCMine_Server.Infrastructure.Server;

namespace TCMine_Server.Infrastructure.Launcher;

/// <summary>
///     Mantém o feed do launcher alinhado à <b>última release do launcher</b> (<c>launcher-v*</c>) sem ação
///     manual: no boot, ao salvar as settings e num <b>poll de hora em hora</b>, tenta recompilar se há uma
///     versão de launcher mais nova que o feed publicado e as settings (URL/AzureId) prontas. Assim uma
///     nova <c>launcher-v*</c> chega aos players com o servidor <b>em execução</b> — sem rebuild de imagem
///     nem reinício do container. O build corre em segundo plano (<see cref="LauncherBuildService" />).
/// </summary>
public sealed class LauncherAutoBuildService(
    LauncherBuildService build,
    ServerSettingsService settings,
    ILogger<LauncherAutoBuildService> logger) : IHostedService, IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);
    private CancellationTokenSource? _cts;

    // O host descarta os hosted services IDisposable no shutdown (após o StopAsync).
    public void Dispose()
    {
        _cts?.Dispose();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = new CancellationTokenSource();
        settings.Changed += OnSettingsChanged;
        _ = PollLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        settings.Changed -= OnSettingsChanged;
        _cts?.Cancel();
        return Task.CompletedTask;
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct); // deixa o boot assentar

            while (!ct.IsCancellationRequested)
            {
                await TryOnceAsync(ct);
                await Task.Delay(PollInterval, ct); // re-checa de hora em hora (nova launcher-v*)
            }
        }
        catch (OperationCanceledException)
        {
            // Encerrando — normal
        }
    }

    private async Task TryOnceAsync(CancellationToken ct)
    {
        try
        {
            if (await build.TryStartAutoAsync(ct))
                logger.LogInformation("Auto-build do launcher iniciado (release nova ou feed desatualizado).");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Auto-build do launcher: verificação falhou.");
        }
    }

    private void OnSettingsChanged(ServerSettings updated)
    {
        _ = TryOnceAsync(CancellationToken.None); // ex.: 1º deploy — settings configuradas agora
    }
}