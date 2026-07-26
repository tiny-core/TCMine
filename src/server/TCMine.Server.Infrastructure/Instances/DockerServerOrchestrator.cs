using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Infrastructure.Docker;

namespace TCMine.Server.Infrastructure.Instances;

/// <summary>
///     Implementa o ciclo de vida sobre containers itzg/minecraft-server. A pasta
///     da instância (materializada) é montada como /data; o mundo vive lá dentro e
///     sobrevive a re-materializações. O container é dono do processo — o TCMine
///     nunca o hospeda em si.
/// </summary>
public sealed class DockerServerOrchestrator(
    DockerApiClient docker,
    IInstanceMaterializer materializer,
    IServerRepository servers,
    IModpackRepository modpacks) : IServerOrchestrator
{
    private const string Image = "itzg/minecraft-server:latest";

    public async Task<string> EnsureCreatedAsync(Guid gameServerId, CancellationToken ct)
    {
        var server = await servers.GetByIdAsync(gameServerId, ct)
                     ?? throw new InvalidOperationException($"Servidor {gameServerId} não encontrado.");

        var version = await modpacks.GetVersionAsync(server.ModpackVersionId, ct)
                      ?? throw new InvalidOperationException("Versão fixada não encontrada.");

        var containerName = $"tcmine-{gameServerId}";

        // 1. Se temos um ContainerId e ele ainda existe, reusa.
        if (server.ContainerId is not null)
        {
            var existing = await docker.InspectContainerAsync(server.ContainerId, ct);
            if (existing is not null)
                return existing.Id;

            // Apontava para um container que já não existe (apagado à mão, por
            // ex.). Limpa a referência morta antes de seguir.
            server.ContainerId = null;
        }

        // 2. Pode existir um container com o nosso nome de uma tentativa anterior
        //    (recria após crash, ContainerId dessincronizado). Remove-o para o
        //    create não colidir por nome.
        await docker.RemoveContainerByNameAsync(containerName, ct);

        // Escreve mods/overrides na pasta da instância (mundo preservado).
        await materializer.MaterializeAsync(gameServerId, version, ct);
        var instancePath = materializer.GetInstancePath(gameServerId);

        // Porta do jogo: extrai do ConnectAddress se tiver ":porta", senão 25565.
        var hostPort = ExtractPort(server.ConnectAddress);

        // A Engine API não puxa no create — garantimos a imagem primeiro.
        // Idempotente: se já está local, o pull retorna rápido.
        await docker.PullImageAsync(Image, ct);

        var spec = new CreateContainerRequest
        {
            Image = Image,
            Env =
            [
                "EULA=TRUE",
                $"TYPE={ItzgEnv.ToServerType(version.Loader)}",
                $"VERSION={version.MinecraftVersion}",
                $"{ItzgEnv.ToServerType(version.Loader)}_VERSION={version.LoaderVersion}",
                $"MEMORY={server.MemoryMb}M",
                $"MAX_PLAYERS={server.MaxPlayers}",
                "ENABLE_RCON=TRUE",
                $"RCON_PASSWORD={server.RconSecret}",
                // itzg não deve gerir mods — nós já materializamos a pasta.
                "REMOVE_OLD_MODS=FALSE"
            ],
            ExposedPorts = new Dictionary<string, object> { ["25565/tcp"] = new() },
            Labels = new Dictionary<string, string> { ["tcmine.server"] = gameServerId.ToString() },
            HostConfig = new HostConfig
            {
                Binds = [$"{instancePath}:/data"],
                Memory = (long)server.MemoryMb * 1024 * 1024,
                PortBindings = new Dictionary<string, PortBinding[]>
                    { ["25565/tcp"] = [new PortBinding { HostPort = hostPort }] },
                RestartPolicy = new RestartPolicy { Name = "unless-stopped" }
            }
        };

        var containerId = await docker.CreateContainerAsync(containerName, spec, ct);

        // Persiste o ContainerId para os próximos ciclos o reencontrarem.
        server.ContainerId = containerId;
        server.UpdatedAt = DateTimeOffset.UtcNow;
        await servers.UpdateAsync(server, ct);

        return containerId;
    }

    public async Task StartAsync(Guid gameServerId, CancellationToken ct)
    {
        var containerId = await EnsureCreatedAsync(gameServerId, ct);
        await docker.StartContainerAsync(containerId, ct);
    }

    public async Task StopAsync(Guid gameServerId, TimeSpan timeout, CancellationToken ct)
    {
        var server = await servers.GetByIdAsync(gameServerId, ct);
        if (server?.ContainerId is null)
            return; // nada a parar

        await docker.StopContainerAsync(server.ContainerId, (int)timeout.TotalSeconds, ct);
    }

    public async Task<GameServerStatus> GetStatusAsync(Guid gameServerId, CancellationToken ct)
    {
        var server = await servers.GetByIdAsync(gameServerId, ct);
        if (server?.ContainerId is null)
            return GameServerStatus.Stopped;

        var inspect = await docker.InspectContainerAsync(server.ContainerId, ct);
        if (inspect is null)
            return GameServerStatus.Stopped; // container sumiu

        return inspect.State switch
        {
            { Running: true } => GameServerStatus.Running,
            { Status: "restarting" } => GameServerStatus.Starting, // loop de reinício
            { Status: "created" } => GameServerStatus.Stopped,
            { ExitCode: not 0 } => GameServerStatus.Crashed,
            _ => GameServerStatus.Stopped
        };
    }

    public IAsyncEnumerable<string> StreamLogsAsync(Guid gameServerId, CancellationToken ct)
    {
        // Streaming do console fica para o sub-passo 4 (protocolo de log do Docker
        // é multiplexado; merece o seu próprio passo).
        throw new NotImplementedException("StreamLogs chega no sub-passo 4.");
    }

    private static string ExtractPort(string connectAddress)
    {
        var idx = connectAddress.LastIndexOf(':');
        return idx >= 0 && int.TryParse(connectAddress[(idx + 1)..], out _)
            ? connectAddress[(idx + 1)..]
            : "25565";
    }
}