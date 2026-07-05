using System.Diagnostics;
using TCMine_Application.Contracts;
using TCMine_Application.Launcher;
using TCMine_Domain.Launcher;

namespace TCMine_Launcher.Infrastructure;

/// <summary>
/// Orquestra a preparação do jogo (sem arrancar): NeoForge (CmlLib) + mods + overrides. Obtém a sessão
/// Minecraft do <see cref="AuthService"/> (ambos na infra). Implementa <see cref="ILaunchOrchestrator"/>.
/// </summary>
public sealed class LaunchOrchestrator(AuthService auth, ServerConfig config) : ILaunchOrchestrator
{
    private readonly GameLauncher _launcher = new();
    private readonly ModInstaller _mods = new();
    private readonly OverridesInstaller _overrides = new(config);
    private readonly PlayerConfigSync _configSync = new(config);

    public async Task<Process> PrepareAsync(
        InstalledModpack instance, ModpackManifestDto manifest, int ramMb, string? javaPath,
        IProgress<LaunchProgress> progress, CancellationToken ct)
    {
        var session = auth.CurrentMSession
                      ?? throw new InvalidOperationException("Sessão inválida — faça login novamente.");

        var gameDir = LauncherPaths.InstanceGameDir(instance.ModpackId);
        var autoJoin = instance.Servers.FirstOrDefault(s => s.Name == instance.AutoJoinServerName);

        var process = await _launcher.PrepareAsync(
            gameDir, instance.Minecraft, instance.NeoForgeVersion, session, ramMb, javaPath,
            progress, ct, instance.Servers, autoJoin);

        // Com overrides não fazemos prune (eles podem trazer jars próprios).
        await _mods.EnsureModsAsync(instance.ModpackId, manifest.Mods, progress, ct, !instance.HasOverrides);

        progress.Report(new LaunchProgress(
            LaunchState.DownloadingAssets, 100, "A aplicar configuração do modpack..."));
        await _overrides.EnsureAsync(instance, ct);

        // Configs do jogador (keybinds/opções/minimapa): puxa do servidor por cima dos overrides, para o
        // jogador reencontrar as suas teclas ao trocar de PC ou após um update. Best-effort — não parte o
        // launch se o servidor estiver indisponível.
        try
        {
            progress.Report(new LaunchProgress(
                LaunchState.DownloadingAssets, 100, "A sincronizar configurações do jogador..."));
            await _configSync.PullAsync(instance, session.UUID ?? "", session.AccessToken ?? "",
                msg => progress.Report(new LaunchProgress(LaunchState.DownloadingAssets, 100, msg)), ct);
        }
        catch
        {
            /* servidor offline ou sem config — segue com as configs locais */
        }

        // Captura do stdout/stderr do jogo para ficheiro (infra) — o chamador só faz Start + BeginRead.
        var log = new GameLogCapture(LauncherPaths.InstanceLogFile(instance.ModpackId));
        process.OutputDataReceived += (_, e) => log.Append(e.Data);
        process.ErrorDataReceived += (_, e) => log.Append(e.Data);
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => log.Dispose();

        return process;
    }

    /// <summary>
    /// Empurra para o servidor as configs player-owned atuais da instância (chamado pela shell ao fechar o
    /// jogo). Atualiza <see cref="InstalledModpack.ConfigSyncedAt"/>; o chamador persiste a instância.
    /// Best-effort — sem sessão/rede válida, é no-op silencioso.
    /// </summary>
    public async Task PushConfigsAsync(
        InstalledModpack instance, Action<string>? report = null, CancellationToken ct = default)
    {
        if (auth.CurrentMSession is not { } session) return;
        await _configSync.PushAsync(instance, session.UUID ?? "", session.AccessToken ?? "", report, ct);
    }
}
