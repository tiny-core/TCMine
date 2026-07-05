using System.Diagnostics;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installer.NeoForge;
using CmlLib.Core.Installer.NeoForge.Installers;
using CmlLib.Core.Installers;
using CmlLib.Core.ProcessBuilder;
using TCMine_Domain.Launcher;

namespace TCMine_Launcher.Infrastructure.Launch;

/// <summary>
/// Instalação e lançamento reais do Minecraft + NeoForge via CmlLib.Core. NÃO chama <c>Start()</c> —
/// devolve o processo pronto. Colaborador interno do <see cref="LaunchOrchestrator"/>.
/// </summary>
internal sealed class GameLauncher
{
    public async Task<Process> PrepareAsync(
        string gameDir, string mcVersion, string neoForgeVersion, MSession session, int ramMb,
        string? javaPath, IProgress<LaunchProgress> progress,
        IReadOnlyList<ModpackServer> servers, ModpackServer? autoJoinServer, CancellationToken ct)
    {
        Directory.CreateDirectory(gameDir);

        if (servers.Count > 0) ServersDatWriter.Ensure(gameDir, servers);

        var launcher = new MinecraftLauncher(gameDir);
        var resolvedJava = string.IsNullOrWhiteSpace(javaPath) ? null : javaPath;

        void OnFileProgress(object? _, InstallerProgressChangedEventArgs e)
        {
            var pct = e.TotalTasks > 0 ? (double)e.ProgressedTasks / e.TotalTasks * 100 : 0;
            progress.Report(new LaunchProgress(
                LaunchState.DownloadingAssets, pct, e.Name ?? "A processar ficheiros..."));
        }

        launcher.FileProgressChanged += OnFileProgress;
        try
        {
            progress.Report(new LaunchProgress(
                LaunchState.InstallingNeoForge, 0, $"A instalar NeoForge {neoForgeVersion}..."));

            var installer = new NeoForgeInstaller(launcher);
            var versionName = await installer.Install(mcVersion, neoForgeVersion, new NeoForgeInstallOptions
            {
                JavaPath = resolvedJava,
                SkipIfAlreadyInstalled = true,
                CancellationToken = ct
            });

            progress.Report(new LaunchProgress(LaunchState.PreparingJvm, 95, "A preparar a JVM..."));

            var launchOption = new MLaunchOption
            {
                Session = session,
                MaximumRamMb = ramMb,
                JavaPath = resolvedJava
            };

            if (autoJoinServer is not null)
            {
                launchOption.ServerIp = autoJoinServer.Address;
                launchOption.ServerPort = autoJoinServer.Port;
            }

            // Encaminha o token: cancelar o launch (CancelLaunch) interrompe o install/build do CmlLib.
            var process = await launcher.InstallAndBuildProcessAsync(versionName, launchOption, cancellationToken: ct);

            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            progress.Report(new LaunchProgress(LaunchState.Launching, 100, "A iniciar o Minecraft..."));
            return process;
        }
        finally
        {
            launcher.FileProgressChanged -= OnFileProgress;
        }
    }
}
