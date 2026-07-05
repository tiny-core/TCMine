using TCMine_Application.Launcher;
using TCMine_Launcher.Infrastructure.Configuration;
using Velopack;

namespace TCMine_Launcher.Infrastructure.Updates;

/// <summary>
///     Implementação Velopack de <see cref="IUpdateService" />: o <see cref="UpdateManager" /> aponta para
///     o feed <c>/updates</c> do TCMine Server (o mesmo que o servidor gera). O canal default no Windows
///     (<c>win</c>) casa com o pack do servidor (<c>-c win</c>), então não é preciso configurá-lo.
///     Guarda internamente a última verificação para o "aplicar" reusar sem re-checar.
/// </summary>
public sealed class UpdateService(ServerConfig config) : IUpdateService
{
    private readonly UpdateManager _manager = new(config.Resolve("/updates").ToString());
    private UpdateInfo? _pending;

    // Feed servido como estáticos em {servidor}/updates

    public bool IsInstalled => _manager.IsInstalled;

    public async Task<string?> CheckAsync(CancellationToken ct = default)
    {
        // Em dev (não instalado via Setup.exe), o UpdateManager não opera — evita exceção
        if (!_manager.IsInstalled) return null;

        _pending = await _manager.CheckForUpdatesAsync();
        return _pending?.TargetFullRelease?.Version.ToString();
    }

    public async Task DownloadAndApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        if (_pending is null) return;

        await _manager.DownloadUpdatesAsync(_pending, p => progress?.Report(p), ct);
        // Aplica e reinicia — a partir daqui o processo é substituído (não retorna)
        _manager.ApplyUpdatesAndRestart(_pending.TargetFullRelease);
    }
}