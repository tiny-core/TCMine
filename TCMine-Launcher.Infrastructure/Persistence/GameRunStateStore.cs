using System.Text.Json;
using TCMine_Application.Launcher;
using TCMine_Launcher.Infrastructure.FileSystem;

namespace TCMine_Launcher.Infrastructure.Persistence;

/// <summary>Persiste o jogo em execução (modpackId + PID). Implementa <see cref="IGameRunStateStore"/>.</summary>
public sealed class GameRunStateStore : IGameRunStateStore
{
    public void Save(string modpackId, int pid)
    {
        try
        {
            LauncherPaths.EnsureRoot();
            File.WriteAllText(LauncherPaths.RunStateFile, JsonSerializer.Serialize(new RunState(modpackId, pid)));
        }
        catch
        {
            // best-effort — a deteção é um extra, não pode partir o launch
        }
    }

    public void Clear()
    {
        try { File.Delete(LauncherPaths.RunStateFile); }
        catch { /* noop */ }
    }

    public RunState? Load()
    {
        try
        {
            return File.Exists(LauncherPaths.RunStateFile)
                ? JsonSerializer.Deserialize<RunState>(File.ReadAllText(LauncherPaths.RunStateFile))
                : null;
        }
        catch
        {
            return null;
        }
    }
}
