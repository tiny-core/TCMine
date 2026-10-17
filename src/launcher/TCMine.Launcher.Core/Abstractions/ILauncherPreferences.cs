namespace TCMine.Launcher.Core.Abstractions;

/// <summary>
///     As escolhas do jogador neste PC (tela Definições). Deste PC, e não da
///     conta: a pergunta que mora aqui hoje — levar também gráficos e som? — é
///     justamente sobre a diferença entre um computador e outro.
/// </summary>
public interface ILauncherPreferences
{
    Task<LauncherPreferences> ReadAsync(CancellationToken ct);

    Task WriteAsync(LauncherPreferences preferences, CancellationToken ct);
}

public sealed record LauncherPreferences
{
    /// <summary>Além das teclas, levar o resto do options.txt (gráficos, som, FOV…).</summary>
    public bool SyncAllOptions { get; init; }
}
