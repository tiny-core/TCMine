using System.Diagnostics;
using TCMine_Application.Contracts;
using TCMine_Domain.Launcher;

namespace TCMine_Application.Launcher;

/// <summary>
/// Porta da preparação do jogo (sem arrancar): instala NeoForge + mods + overrides e devolve o
/// <see cref="Process"/> pronto. A implementação (infra) obtém a sessão Minecraft do login.
/// </summary>
public interface ILaunchOrchestrator
{
    Task<Process> PrepareAsync(
        InstalledModpack instance, ModpackManifestDto manifest, int ramMb, string? javaPath,
        IProgress<LaunchProgress> progress, CancellationToken ct);
}
