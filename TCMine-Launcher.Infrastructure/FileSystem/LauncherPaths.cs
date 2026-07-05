namespace TCMine_Launcher.Infrastructure.FileSystem;

/// <summary>Diretórios de dados do launcher, sob <c>%AppData%/TCMine</c>.</summary>
public static class LauncherPaths
{
    private static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TCMine");

    public static string SessionFile => Path.Combine(Root, "session.bin");
    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string RunStateFile => Path.Combine(Root, "running.json");
    public static string InstancesDir => Path.Combine(Root, "instances");
    public static string ModCacheDir => Path.Combine(Root, "cache", "mods");
    public static string ImageCacheDir => Path.Combine(Root, "cache", "images");

    public static string InstanceDir(string id)
    {
        return Path.Combine(InstancesDir, id);
    }

    public static string InstanceConfigFile(string id)
    {
        return Path.Combine(InstanceDir(id), "instance.json");
    }

    public static string InstanceGameDir(string id)
    {
        return Path.Combine(InstanceDir(id), "minecraft");
    }

    public static string InstanceLogFile(string id)
    {
        return Path.Combine(InstanceDir(id), "logs", "latest.log");
    }

    public static void EnsureRoot()
    {
        Directory.CreateDirectory(Root);
    }
}