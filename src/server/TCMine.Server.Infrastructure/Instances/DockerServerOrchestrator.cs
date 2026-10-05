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

    /// <summary>Label com a impressão digital da spec (ver <see cref="ItzgEnv.Fingerprint" />).</summary>
    private const string SpecLabel = "tcmine.spec";

    public async Task<string> EnsureCreatedAsync(Guid gameServerId, CancellationToken ct)
    {
        var server = await servers.GetByIdAsync(gameServerId, ct)
                     ?? throw new InvalidOperationException($"Servidor {gameServerId} não encontrado.");

        // MC e loader agora vivem no modpack. Carrega para o itzg (TYPE/VERSION).
        var modpack = await modpacks.GetByIdAsync(server.ModpackId, ct)
                      ?? throw new InvalidOperationException($"Modpack {server.ModpackId} não encontrado.");

        var version = await modpacks.GetVersionAsync(server.ModpackVersionId, ct)
                      ?? throw new InvalidOperationException("Versão fixada não encontrada.");

        var containerName = $"tcmine-{gameServerId}";

        // Porta do jogo: extrai do ConnectAddress se tiver ":porta", senão 25565.
        var hostPort = ExtractPort(server.ConnectAddress);
        var instancePath = materializer.GetInstancePath(gameServerId);

        // O que decide QUE jogo roda vem do modpack (Minecraft e loader, fixos)
        // e da versão fixada no servidor (build do loader).
        var gameEnv = ItzgEnv.GameVariables(modpack.MinecraftVersion, modpack.Loader, version.LoaderVersion);
        IReadOnlyList<string> settings =
        [
            .. gameEnv,
            $"MEMORY={server.MemoryMb}M",
            $"MAX_PLAYERS={server.MaxPlayers}"
        ];

        // A senha do RCON fica FORA da impressão digital: ela não muda, e um
        // label é legível por qualquer um com acesso ao Docker.
        var fingerprint = ItzgEnv.Fingerprint(Image, settings, [$"port={hostPort}", $"bind={instancePath}"]);

        // 1. Se temos um ContainerId e ele ainda existe, decide entre reusar e
        //    recriar. Reusar SEMPRE era o defeito: o ambiente de um container é
        //    fixado no create, então trocar a versão do servidor (ou a memória)
        //    não chegava ao jogo — ele continuava com o TYPE/VERSION do dia em
        //    que o container nasceu, e a pasta nem era rematerializada.
        if (server.ContainerId is not null)
        {
            var existing = await docker.InspectContainerAsync(server.ContainerId, ct);

            // Rodando: não se mexe. Reescrever mods ou recriar o container
            // debaixo de quem está jogando derrubaria a sessão.
            if (existing is { State.Running: true })
                return existing.Id;

            if (existing is not null
                && existing.Config.Labels?.GetValueOrDefault(SpecLabel) == fingerprint)
            {
                // Mesma spec: só traz a pasta para a versão fixada (o
                // materializador é idempotente e preserva o mundo).
                await materializer.MaterializeAsync(gameServerId, version, ct);
                return existing.Id;
            }

            // Apontava para um container que já não existe (apagado à mão, por
            // ex.) ou cuja spec envelheceu. Cai para a recriação abaixo.
            server.ContainerId = null;
        }

        // 2. Pode existir um container com o nosso nome de uma tentativa anterior
        //    (recria após crash, ContainerId dessincronizado, spec antiga).
        //    Remove-o para o create não colidir por nome. O mundo vive na pasta
        //    montada, não no container — recriar não perde nada.
        await docker.RemoveContainerByNameAsync(containerName, ct);

        // Escreve mods/overrides na pasta da instância (mundo preservado).
        await materializer.MaterializeAsync(gameServerId, version, ct);

        // A Engine API não puxa no create — garantimos a imagem primeiro.
        // Idempotente: se já está local, o pull retorna rápido.
        await docker.PullImageAsync(Image, ct);

        var spec = new CreateContainerRequest
        {
            Image = Image,
            Env =
            [
                "EULA=TRUE",
                .. settings,
                "ENABLE_RCON=TRUE",
                $"RCON_PASSWORD={server.RconSecret}",
                // itzg não deve gerir mods — nós já materializamos a pasta.
                "REMOVE_OLD_MODS=FALSE",

                // O mesmo usuário dos dois lados. A pasta da instância é escrita
                // pelo usuário deste container; a imagem itzg roda como 1000 e,
                // sem ser root, não consegue corrigir o dono. Sem isto o
                // servidor de jogo morre com "permission denied" no próprio
                // /data — um diretório que existe e cujo dono parece certo.
                .. UsuarioDoProcesso()
            ],
            ExposedPorts = new Dictionary<string, object> { ["25565/tcp"] = new() },
            Labels = new Dictionary<string, string>
            {
                ["tcmine.server"] = gameServerId.ToString(),
                [SpecLabel] = fingerprint
            },
            HostConfig = new HostConfig
            {
                Binds = [$"{instancePath}:/data"],
                Memory = (long)server.MemoryMb * 1024 * 1024,
                PortBindings =
                    new Dictionary<string, PortBinding[]> { ["25565/tcp"] = [new PortBinding { HostPort = hostPort }] },
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

    /// <summary>
    ///     UID/GID para a imagem itzg, quando dá para saber.
    ///     Fora do Linux não se aplica: no Docker Desktop o bind mount não
    ///     carrega dono de Unix, e mandar números aí só confundiria.
    /// </summary>
    private static string[] UsuarioDoProcesso() =>
        ProcessUser.Current is { } user ? [$"UID={user.Uid}", $"GID={user.Gid}"] : [];

    public async Task RemoveAsync(Guid gameServerId, CancellationToken ct)
    {
        // force=true para no RemoveContainerByNameAsync: para e remove mesmo que
        // esteja a correr. Ao apagar o servidor, é o comportamento esperado.
        await docker.RemoveContainerByNameAsync($"tcmine-{gameServerId}", ct);
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

        ContainerInspect? inspect;
        try
        {
            inspect = await docker.InspectContainerAsync(server.ContainerId, ct);
        }
        catch (Exception ex)
        {
            // Não deixa uma falha de inspect derrubar a reconciliação da página.
            Console.WriteLine($"[Docker] inspect falhou para {server.ContainerId}: {ex.Message}");
            return server.Status; // mantém o último conhecido
        }

        if (inspect is null)
            return GameServerStatus.Stopped;

        Console.WriteLine(
            $"[Docker] {server.ContainerId[..12]} Running={inspect.State.Running} Status={inspect.State.Status} Exit={inspect.State.ExitCode}");

        return inspect.State switch
        {
            { Running: true } => GameServerStatus.Running,
            { Status: "restarting" } => GameServerStatus.Starting,
            { Status: "created" } => GameServerStatus.Stopped,
            { ExitCode: not 0 } => GameServerStatus.Crashed,
            _ => GameServerStatus.Stopped
        };
    }

    public IAsyncEnumerable<ConsoleLine> StreamLogsAsync(Guid gameServerId, CancellationToken ct)
    {
        // 200 linhas de histórico: o bastante para ver por que o servidor caiu
        // sem despejar o log inteiro de uma sessão de horas no circuito.
        return docker.StreamLogsAsync($"tcmine-{gameServerId}", 200, ct);
    }

    private static string ExtractPort(string connectAddress)
    {
        var idx = connectAddress.LastIndexOf(':');
        return idx >= 0 && int.TryParse(connectAddress[(idx + 1)..], out _)
            ? connectAddress[(idx + 1)..]
            : "25565";
    }
}
