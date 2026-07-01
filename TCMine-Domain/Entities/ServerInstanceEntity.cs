using System.ComponentModel.DataAnnotations;

namespace TCMine_Domain.Entities;

/// <summary>Estado do ciclo de vida de um servidor Minecraft gerenciado.</summary>
public enum ServerInstanceStatus
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Crashed,

    /// <summary>
    ///     Provisionamento em andamento (montando loader/mods/configs). Persistido para que um reinício do
    ///     TCMine-Server no meio do processo possa **retomar** a provisão (ver ProvisioningCoordinator).
    ///     Guardado como texto na coluna Status (conversão de string), então não exige migration.
    /// </summary>
    Provisioning
}

/// <summary>
/// Uma instância de servidor Minecraft derivada de um modpack. Reúne os mods do lado
/// servidor (ver <see cref="TCMine_Domain.Modpack.ModSide"/>), o loader e as configs num diretório
/// próprio, executado num <b>container Docker dedicado</b> (Docker-out-of-Docker: o TCMine-Server
/// fala com o daemon do host via socket). O container roda uma imagem própria só-Java
/// (controle total: o TCMine baixa o loader, monta mods e gera as configs).
///
/// Esta entidade é só <b>persistência</b>: a orquestração (provisionar o diretório, criar/iniciar/
/// parar o container, capturar logs) vem em etapas posteriores. Os campos de runtime
/// (<see cref="Status"/>, <see cref="ContainerId"/>) refletem o último estado conhecido.
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
    /// RAM máxima alocada ao processo Java (MB) — vira o <c>-Xmx</c> da JVM.
    /// </summary>
    public int RamMb { get; set; } = 4096;

    /// <summary>
    /// Heap inicial da JVM (<c>-Xms</c>, em MB). <c>0</c> = usa o mesmo valor de <see cref="RamMb"/>
    /// (recomendado para servidores: Xms == Xmx evita realocações de heap em runtime).
    /// </summary>
    public int XmsMb { get; set; }

    /// <summary>
    /// Flags extras de JVM além de memória (ex.: flags Aikar/G1GC), uma por linha. O provisioner as
    /// escreve no <c>user_jvm_args.txt</c> da instância. Vazio = só os defaults gerados pelo TCMine.
    /// </summary>
    [MaxLength(2000)]
    public string ExtraJvmArgs { get; set; } = string.Empty;

    public int MaxPlayers { get; set; } = 20;

    [MaxLength(120)] public string Motd { get; set; } = "A TCMine server";

    /// <summary>
    /// Endereço público (host/IP) que os jogadores usam para conectar — base da <b>auto-divulgação</b>
    /// no launcher. Vazio = não dá para divulgar (sem endereço conhecido). Combinado com <see cref="Port"/>.
    /// </summary>
    [MaxLength(200)]
    public string PublicAddress { get; set; } = string.Empty;

    /// <summary>
    /// Divulgar automaticamente esta instância na lista multiplayer do launcher (gera/atualiza um
    /// <see cref="ServerEntryEntity"/> do modpack). Só tem efeito com <see cref="PublicAddress"/> preenchido.
    /// </summary>
    public bool Advertise { get; set; } = true;

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
    /// ID do container Docker que roda esta instância, enquanto existe (null = sem container criado).
    /// É o handle de runtime no modelo Docker-out-of-Docker — substitui o antigo PID de processo.
    /// </summary>
    [MaxLength(64)]
    public string? ContainerId { get; set; }

    /// <summary>
    /// Tag da imagem Docker usada para rodar a instância (ex.: <c>"eclipse-temurin:25-jre"</c>).
    /// Guardada para reproduzir/depurar o ambiente exato em que o container subiu.
    /// </summary>
    [MaxLength(120)]
    public string? ImageTag { get; set; }

    /// <summary>
    /// Última provisão concluída com sucesso (UTC); <c>null</c> = nunca provisionada. O provisioner
    /// monta o diretório (loader, mods, configs) e marca aqui; o painel usa para saber se a instância
    /// está pronta para subir e se precisa re-provisionar após mudanças no modpack.
    /// </summary>
    public DateTime? ProvisionedAt { get; set; }

    /// <summary>
    /// Reinicia automaticamente após crash/parada inesperada
    /// </summary>
    public bool AutoRestart { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}