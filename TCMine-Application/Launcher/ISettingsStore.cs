using TCMine_Domain.Launcher;

namespace TCMine_Application.Launcher;

/// <summary>Persistência das definições globais do launcher.</summary>
public interface ISettingsStore
{
    LauncherSettings Load();
    void Save(LauncherSettings settings);
}

/// <summary>Informação do sistema (hoje: RAM física, para limitar o slider de memória).</summary>
public interface ISystemInfo
{
    int TotalPhysicalRamMb { get; }
}
