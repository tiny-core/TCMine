namespace TCMine_Launcher.Services;

/// <summary>Diretórios de dados do launcher, sob <c>%AppData%/TCMine</c>.</summary>
public static class LauncherPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TCMine");

    /// <summary>Sessão TCMine persistida (cifrada) entre execuções.</summary>
    public static string SessionFile => Path.Combine(Root, "session.bin");

    public static void EnsureCreated() => Directory.CreateDirectory(Root);
}
