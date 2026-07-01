using TCMine_Domain.Launcher;

namespace TCMine_Application.Launcher;

/// <summary>Persistência das instâncias instaladas (uma pasta por modpack).</summary>
public interface IInstanceStore
{
    IReadOnlyList<InstalledModpack> LoadAll();
    InstalledModpack? Load(string modpackId);
    void Save(InstalledModpack instance);
    bool IsRegistered(string modpackId);
    void Delete(string modpackId);

    /// <summary>Pasta da instância (config + jogo).</summary>
    string InstanceDir(string modpackId);

    /// <summary>Pasta <c>.minecraft</c> da instância (mods, shaderpacks, resourcepacks…).</summary>
    string GameDir(string modpackId);

    /// <summary>Exporta a instância (config + jogo) para um zip.</summary>
    void Export(string modpackId, string zipPath);

    /// <summary>Importa uma instância de um zip e devolve os seus metadados (ou null se inválido).</summary>
    InstalledModpack? Import(string zipPath);
}

/// <summary>Estado do jogo em execução (para detetar um jogo aberto ao reabrir o launcher).</summary>
public sealed record RunState(string ModpackId, int Pid);

/// <summary>Persiste qual instância tem o jogo a correr (modpackId + PID).</summary>
public interface IGameRunStateStore
{
    void Save(string modpackId, int pid);
    void Clear();
    RunState? Load();
}
