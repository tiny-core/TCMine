using System.Text.Json;
using TCMine_Application.Launcher;
using TCMine_Domain.Launcher;
using TCMine_Launcher.Infrastructure.FileSystem;

namespace TCMine_Launcher.Infrastructure.Persistence;

/// <summary>Persiste as <see cref="LauncherSettings"/> em <c>settings.json</c> (escrita atómica).</summary>
public sealed class SettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public LauncherSettings Load()
    {
        try
        {
            if (File.Exists(LauncherPaths.SettingsFile))
            {
                var settings = JsonSerializer.Deserialize<LauncherSettings>(
                    File.ReadAllText(LauncherPaths.SettingsFile), Options);
                if (settings is not null) return settings;
            }
        }
        catch
        {
            // corrompido/ilegível — segue com os defaults
        }

        return new LauncherSettings();
    }

    public void Save(LauncherSettings settings)
    {
        try
        {
            LauncherPaths.EnsureRoot();
            var tmp = LauncherPaths.SettingsFile + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(settings, Options));
            File.Move(tmp, LauncherPaths.SettingsFile, true);
        }
        catch
        {
            // best-effort
        }
    }
}
