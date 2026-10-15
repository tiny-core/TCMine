using TCMine.Launcher.Core.Modpacks;
using TCMine.UI.Shared.Formatting;

namespace TCMine.Launcher.UI.Components;

/// <summary>
///     O que uma instalação está a fazer agora, em palavras. Uma porcentagem
///     sozinha não distingue "baixando 400 MB" de "ligando 300 arquivos", e as
///     duas fases têm velocidades muito diferentes — sem o rótulo, a barra
///     parece travar. Partilhado pelos cards do catálogo e dos servidores.
/// </summary>
public static class InstallPhaseText
{
    public static string Of(InstallProgress? progress) => progress?.Phase switch
    {
        InstallPhase.Planning => "A comparar com o que já está no disco…",
        InstallPhase.Downloading =>
            $"A baixar {HumanSize.Bytes(progress.BytesDone)} de {HumanSize.Bytes(progress.BytesTotal)}",
        InstallPhase.Materializing => $"A instalar {progress.FilesDone} de {progress.FilesTotal} arquivos",
        InstallPhase.Cleaning => "A remover o que sobrou da versão anterior…",
        InstallPhase.Done => "Pronto.",
        _ => "A preparar…"
    };
}
