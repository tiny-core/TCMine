using System.ComponentModel.DataAnnotations;

namespace TCMine_Domain.Entities;

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
/// servidor (ver <see cref="TCMine_Domain.Modpack.ModSide"/>), o loader e as configs num diretório
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

    /// <summary>
    /// Modpack de origem (mods do lado servidor, loader, versões)
    /// </summary>
    public Guid ModpackId { get; set; }

    /// <summary>
    /// Representa o modpack associado a uma instância de servidor.
    /// Relaciona-se com a entidade <see cref="ModpackEntity"/>, que contém os dados
    /// fundamentais do modpack, como versões, loader e mods.
    /// </summary>
    public ModpackEntity? Modpack { get; set; }

    /// <summary>
    /// Porta utilizada pela instância de servidor Minecraft para aceitar conexões de jogadores.
    /// É o ponto de entrada da rede para o servidor na máquina host.
    /// </summary>
    public int Port { get; set; } = 25565;

    /// <summary>
    /// RAM máxima alocada ao processo Java (MB)
    /// </summary>
    public int RamMb { get; set; } = 4096;

    public int MaxPlayers { get; set; } = 20;

    [MaxLength(120)] public string Motd { get; set; } = "A TCMine server";

    /// <summary>
    /// Diretório de trabalho da instância (sob ServerPaths.Servers / {ID})
    /// </summary>
    [MaxLength(400)]
    public string Directory { get; set; } = string.Empty;

    /// <summary>
    /// Representa o estado atual de execução de uma instância de servidor Minecraft.
    /// Esse campo reflete o último estado conhecido, podendo ser:
    /// <code>
    /// - Stopped:  O servidor está parado.
    /// - Starting: O servidor está em processo de inicialização.
    /// - Running:  O servidor está em execução.
    /// - Stopping: O servidor está em processo de parada.
    /// - Crashed:  O servidor sofreu uma falha inesperada.
    /// </code>
    /// </summary>
    public ServerInstanceStatus Status { get; set; } = ServerInstanceStatus.Stopped;

    /// <summary>
    /// PID do processo Java, enquanto em execução (null = parado)
    /// </summary>
    public int? Pid { get; set; }

    /// <summary>
    /// Reinicia automaticamente após crash/parada inesperada
    /// </summary>
    public bool AutoRestart { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}