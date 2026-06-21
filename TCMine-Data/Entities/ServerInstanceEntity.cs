using System.ComponentModel.DataAnnotations;

namespace TCMine_Data.Entities;

/// <summary>Estado do ciclo de vida de um servidor Minecraft gerenciado.</summary>
public enum ServerInstanceStatus
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Crashed
}

/// <summary>
/// Uma instância de servidor Minecraft derivada de um modpack. Reúne os mods do lado
/// servidor (ver <see cref="TCMine_Core.modpack.ModSide"/>), o loader e as configs num diretório
/// próprio, executado como um processo Java no host (decisão de projeto).
///
/// Esta entidade é só <b>persistência</b>: a orquestração (baixar NeoForge, montar mods,
/// iniciar/parar o processo, capturar logs) virá numa etapa posterior. Os campos de runtime
/// (<see cref="Status"/>, <see cref="Pid"/>) refletem o último estado conhecido.
/// </summary>
public class ServerInstanceEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(120)] public string Name { get; set; } = string.Empty;

    // Modpack de origem (mods do lado servidor, loader, versões)
    [MaxLength(80)] public string ModpackId { get; set; } = string.Empty;
    public ModpackEntity? Modpack { get; set; }

    public int Port { get; set; } = 25565;

    // RAM máxima alocada ao processo Java (MB)
    public int RamMb { get; set; } = 4096;

    public int MaxPlayers { get; set; } = 20;

    [MaxLength(120)] public string Motd { get; set; } = "A TCMine server";

    // Diretório de trabalho da instância (sob ServerPaths.Servers / {ID})
    [MaxLength(400)] public string Directory { get; set; } = string.Empty;

    public ServerInstanceStatus Status { get; set; } = ServerInstanceStatus.Stopped;

    // PID do processo Java, enquanto em execução (null = parado)
    public int? Pid { get; set; }

    // Reinicia automaticamente após crash/parada inesperada
    public bool AutoRestart { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}