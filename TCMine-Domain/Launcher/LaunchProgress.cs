namespace TCMine_Domain.Launcher;

/// <summary>Fases da preparação/lançamento (lógica de domínio, não de UI).</summary>
public enum LaunchState
{
    Idle,
    CheckingFiles,
    DownloadingAssets,
    InstallingNeoForge,
    PreparingJvm,
    Launching,
    Running,
    Failed
}

/// <summary>Snapshot imutável do progresso do launch num dado momento.</summary>
public record LaunchProgress(LaunchState State, double Percent, string Message)
{
    public static LaunchProgress Idle => new(LaunchState.Idle, 0, "Pronto");

    public bool IsActive => State is not (LaunchState.Idle or LaunchState.Failed);
}
