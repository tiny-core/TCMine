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

    /// <summary>
    /// Empurra para o servidor as configs player-owned (keybinds/opções/minimapa) da instância. Chamado
    /// ao fechar o jogo, para o estado ficar disponível noutros PCs. Atualiza
    /// <see cref="InstalledModpack.ConfigSyncedAt"/>; o chamador deve persistir a instância.
    /// <paramref name="report"/> recebe mensagens de status (ex.: "A enviar configurações…") para a UI.
    /// </summary>
    Task PushConfigsAsync(InstalledModpack instance, Action<string>? report = null, CancellationToken ct = default);
}
