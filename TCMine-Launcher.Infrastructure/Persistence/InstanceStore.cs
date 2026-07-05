using System.IO.Compression;
using System.Text.Json;
using TCMine_Application.Launcher;
using TCMine_Domain.Launcher;
using TCMine_Launcher.Infrastructure.FileSystem;

namespace TCMine_Launcher.Infrastructure.Persistence;

/// <summary>Persistência das instâncias instaladas (uma pasta por modpack). Implementa <see cref="IInstanceStore" />.</summary>
public sealed class InstanceStore : IInstanceStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public IReadOnlyList<InstalledModpack> LoadAll()
    {
        var result = new List<InstalledModpack>();
        if (!Directory.Exists(LauncherPaths.InstancesDir)) return result;

        foreach (var dir in Directory.EnumerateDirectories(LauncherPaths.InstancesDir))
            if (Load(Path.GetFileName(dir)) is { } instance)
                result.Add(instance);

        return result;
    }

    public InstalledModpack? Load(string modpackId)
    {
        try
        {
            var path = LauncherPaths.InstanceConfigFile(modpackId);
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<InstalledModpack>(File.ReadAllText(path), Options);
        }
        catch
        {
            return null;
        }
    }

    public void Save(InstalledModpack instance)
    {
        try
        {
            Directory.CreateDirectory(LauncherPaths.InstanceDir(instance.ModpackId));
            var path = LauncherPaths.InstanceConfigFile(instance.ModpackId);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(instance, Options));
            File.Move(tmp, path, true);
        }
        catch
        {
            // best-effort
        }
    }

    public bool IsRegistered(string modpackId)
    {
        return File.Exists(LauncherPaths.InstanceConfigFile(modpackId));
    }

    public void Delete(string modpackId)
    {
        try
        {
            var dir = LauncherPaths.InstanceDir(modpackId);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
        catch
        {
            /* noop */
        }
    }

    public string InstanceDir(string modpackId)
    {
        return LauncherPaths.InstanceDir(modpackId);
    }

    public string GameDir(string modpackId)
    {
        return LauncherPaths.InstanceGameDir(modpackId);
    }

    public void Export(string modpackId, string zipPath)
    {
        var dir = LauncherPaths.InstanceDir(modpackId);
        if (!Directory.Exists(dir)) return;
        if (File.Exists(zipPath)) File.Delete(zipPath);
        ZipFile.CreateFromDirectory(dir, zipPath, CompressionLevel.Optimal, false);
    }

    public InstalledModpack? Import(string zipPath)
    {
        // Lê os metadados primeiro (instance.json na raiz do zip) para saber a pasta de destino.
        InstalledModpack? meta;
        using (var zip = ZipFile.OpenRead(zipPath))
        {
            var entry = zip.GetEntry("instance.json");
            if (entry is null) return null;
            using var stream = entry.Open();
            meta = JsonSerializer.Deserialize<InstalledModpack>(stream, Options);
        }

        if (meta is null || string.IsNullOrWhiteSpace(meta.ModpackId)) return null;

        var dir = LauncherPaths.InstanceDir(meta.ModpackId);
        Directory.CreateDirectory(dir);
        ZipFile.ExtractToDirectory(zipPath, dir, true);
        return Load(meta.ModpackId);
    }
}