using System.Runtime.CompilerServices;
using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.EntityFrameworkCore;
using TCMine_Domain.Entities;
using TCMine_Server.Infrastructure.Persistence;

namespace TCMine_Server.Infrastructure.ServerInstances;

/// <summary>
///     Ciclo de vida do container Docker de uma instância de servidor Minecraft (Docker-out-of-Docker):
///     cria/inicia/para/remove o container, envia comandos pelo stdin (console) e transmite os logs.
///     O container roda a imagem só-Java do TCMine com o diretório provisionado da instância montado em
///     <c>/data</c>; o comando de início vem de <see cref="ServerRuntimeInstaller.ResolveLaunchArgs" />
///     (derivado do layout do install em cache). O estado conhecido (Status/ContainerId) é refletido na BD.
///     Pré-requisito: a instância já provisionada (<see cref="ServerProvisioner" />) — este serviço não
///     monta o diretório, só executa.
/// </summary>
public sealed class DockerMinecraftManager(AppDbContext db, DockerEnvironment docker)
{
    private const string WorkDir = "/data";

    // Trava global de início: garante que só um servidor sobe por vez (evita pico de recursos e corridas
    // ao criar/iniciar containers em paralelo). Estática = compartilhada por todos os circuitos/requisições.
    private static readonly SemaphoreSlim StartGate = new(1, 1);

    private DockerClient Client => docker.Client;

    // Nome estável do container por instância — permite reconciliar estado entre reinícios do servidor
    private static string ContainerName(Guid instanceId)
    {
        return $"tcmine-mc-{instanceId}";
    }

    // ── Start ───────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Garante o container criado e em execução para a instância. Idempotente: reaproveita um container
    ///     existente (inicia se parado) e cria um novo se não houver. Atualiza Status/ContainerId na BD.
    /// </summary>
    public async Task StartAsync(Guid instanceId, CancellationToken ct = default)
    {
        var instance = await LoadAsync(instanceId, ct);
        if (instance.ProvisionedAt is null)
            throw new InvalidOperationException("Instância não provisionada — provisione antes de iniciar.");

        // Trava: recusa se outro servidor já está sendo iniciado agora (não bloqueia em fila — avisa)
        if (!await StartGate.WaitAsync(0, ct))
            throw new InvalidOperationException(
                "Outro servidor está sendo iniciado neste momento — aguarde ele subir e tente de novo.");

        try
        {
            await SetStatusAsync(instance, ServerInstanceStatus.Starting, ct);

            // Reaproveita um container já existente (por nome) — evita duplicar ou perder um em execução
            var existing = await FindByNameAsync(ContainerName(instanceId), ct);
            var containerId = existing?.ID ?? await CreateContainerAsync(instance, ct);

            await Client.Containers.StartContainerAsync(containerId, new ContainerStartParameters(), ct);

            instance.ContainerId = containerId;
            await SetStatusAsync(instance, ServerInstanceStatus.Running, ct);
        }
        finally
        {
            StartGate.Release();
        }
    }

    // Cria o container a partir do estado provisionado da instância (mods/configs já em disco)
    private async Task<string> CreateContainerAsync(ServerInstanceEntity instance, CancellationToken ct)
    {
        var modpack = instance.Modpack
                      ?? throw new InvalidOperationException("Instância sem modpack de origem.");

        var launchArgs = ServerRuntimeInstaller.ResolveLaunchArgs(modpack.Loader, modpack.LoaderVersion);
        var hostDir = docker.ToHostPath(instance.Directory);
        var port = instance.Port.ToString();
        var portKey = $"{port}/tcp";

        var image = string.IsNullOrWhiteSpace(instance.ImageTag) ? docker.McImage : instance.ImageTag;
        await docker.EnsureImageAsync(image, ct);

        var create = new CreateContainerParameters
        {
            Image = image,
            Name = ContainerName(instance.Id),
            // Entrypoint explícito = java: sobrepõe o "dotnet" da imagem do release (reuso de imagem)
            Entrypoint = ["java"],
            Cmd = [.. launchArgs],
            WorkingDir = WorkDir,
            // stdin aberto e anexável → conseguimos enviar comandos ao console do servidor
            OpenStdin = true,
            Tty = false,
            ExposedPorts = new Dictionary<string, EmptyStruct> { [portKey] = default },
            HostConfig = new HostConfig
            {
                // Mounts (objeto) em vez de Binds (string "origem:destino"): evita a colisão do ':' do
                // drive Windows (P:\…) com o separador do bind. Ver DockerServerJavaRunner.
                Mounts = [new Mount { Type = "bind", Source = hostDir, Target = WorkDir }],
                // Mapeia a mesma porta host↔container (server.properties usa instance.Port)
                PortBindings = new Dictionary<string, IList<PortBinding>>
                {
                    [portKey] = [new PortBinding { HostPort = port }]
                },
                // Reinício automático após crash, se a instância pedir
                RestartPolicy = new RestartPolicy
                {
                    Name = instance.AutoRestart ? RestartPolicyKind.UnlessStopped : RestartPolicyKind.No
                }
            }
        };

        // Anexa a uma rede dedicada, se configurada (ex.: para um proxy/reverse na frente)
        if (docker.Network is { } net)
            create.NetworkingConfig = new NetworkingConfig
            {
                EndpointsConfig = new Dictionary<string, EndpointSettings> { [net] = new() }
            };

        var response = await Client.Containers.CreateContainerAsync(create, ct);
        return response.ID;
    }

    // ── Stop / Remove ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Para o servidor graciosamente: envia <c>stop</c> pelo console e, em seguida, garante a parada do
    ///     container (com prazo antes do kill). Atualiza Status na BD.
    /// </summary>
    public async Task StopAsync(Guid instanceId, CancellationToken ct = default)
    {
        var instance = await LoadAsync(instanceId, ct);
        if (instance.ContainerId is not { } containerId) return;

        await SetStatusAsync(instance, ServerInstanceStatus.Stopping, ct);

        // Tenta o desligamento limpo do mundo via comando do jogo; ignora se o stdin já não responde
        try
        {
            await SendRawAsync(containerId, "stop\n", ct);
        }
        catch
        {
            /* cai para o stop do container */
        }

        // Dá tempo do save antes de matar (SIGTERM → kill). 90s cobre saves grandes de modpacks pesados.
        await Client.Containers.StopContainerAsync(containerId,
            new ContainerStopParameters { WaitBeforeKillSeconds = 90 }, ct);

        await SetStatusAsync(instance, ServerInstanceStatus.Stopped, ct);
    }

    /// <summary>Remove o container da instância (para + remove). O diretório provisionado permanece.</summary>
    public async Task RemoveContainerAsync(Guid instanceId, CancellationToken ct = default)
    {
        var instance = await LoadAsync(instanceId, ct);
        var containerId = instance.ContainerId ?? (await FindByNameAsync(ContainerName(instanceId), ct))?.ID;
        if (containerId is null) return;

        await Client.Containers.RemoveContainerAsync(containerId,
            new ContainerRemoveParameters { Force = true }, ct);

        instance.ContainerId = null;
        await SetStatusAsync(instance, ServerInstanceStatus.Stopped, ct);
    }

    // ── Reconciliação de status ───────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Concilia o estado conhecido (Running/Starting) com a realidade do daemon: se o container saiu
    ///     inesperadamente, marca <c>Crashed</c>; se sumiu, <c>Stopped</c>; se está de pé, <c>Running</c>.
    ///     Só examina instâncias que a BD acha ativas (paradas via <see cref="StopAsync" /> já estão certas).
    ///     Uma única chamada ao Docker (lista por prefixo de nome). Devolve true se algo mudou.
    /// </summary>
    public async Task<bool> ReconcileAllAsync(CancellationToken ct = default)
    {
        var active = await db.ServerInstances
            .Where(i => i.Status == ServerInstanceStatus.Running || i.Status == ServerInstanceStatus.Starting)
            .ToListAsync(ct);
        if (active.Count == 0) return false;

        IList<ContainerListResponse> containers;
        try
        {
            containers = await Client.Containers.ListContainersAsync(new ContainersListParameters
            {
                All = true,
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["name"] = new Dictionary<string, bool> { ["tcmine-mc-"] = true }
                }
            }, ct);
        }
        catch
        {
            return false; // daemon indisponível → não mexe no estado conhecido
        }

        var changed = false;
        foreach (var inst in active)
        {
            var name = ContainerName(inst.Id);
            var container = containers.FirstOrDefault(c => c.Names.Any(n => n.TrimStart('/') == name));

            ServerInstanceStatus status;
            if (container is null)
            {
                // Container sumiu (removido) → parado; limpa o handle
                inst.ContainerId = null;
                status = ServerInstanceStatus.Stopped;
            }
            else if (string.Equals(container.State, "running", StringComparison.OrdinalIgnoreCase))
            {
                status = ServerInstanceStatus.Running;
            }
            else
            {
                // A BD achava ativo, mas o container saiu — saída inesperada (parada limpa já marca Stopped)
                status = ServerInstanceStatus.Crashed;
            }

            if (status != inst.Status)
            {
                inst.Status = status;
                inst.UpdatedAt = DateTime.UtcNow;
                changed = true;
            }
        }

        if (changed) await db.SaveChangesAsync(ct);
        return changed;
    }

    // ── Console (stdin) ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Envia um comando ao console do servidor (ex.: <c>say</c>, <c>op</c>, <c>whitelist reload</c>).
    ///     Recebe o <paramref name="containerId" /> direto (não toca na BD) para poder rodar concorrente ao
    ///     stream de logs sem disputar o DbContext scoped do circuito Blazor.
    /// </summary>
    public async Task SendCommandAsync(string containerId, string command, CancellationToken ct = default)
    {
        await SendRawAsync(containerId, command.TrimEnd('\n') + "\n", ct);
    }

    // Anexa ao stdin do container e escreve a linha; o stream é fechado logo após (one-shot)
    private async Task SendRawAsync(string containerId, string text, CancellationToken ct)
    {
        using var stream = await Client.Containers.AttachContainerAsync(containerId, false,
            new ContainerAttachParameters { Stream = true, Stdin = true }, ct);
        var bytes = Encoding.UTF8.GetBytes(text);
        await stream.WriteAsync(bytes, 0, bytes.Length, ct);
        // Sinaliza fim do lado de escrita (stdin) — entrega a linha ao processo do servidor
        stream.CloseWrite();
    }

    // ── Logs ──────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Transmite os logs do container (stdout+stderr) como blocos de texto, opcionalmente seguindo em
    ///     tempo real. Recebe o <paramref name="containerId" /> direto (não toca na BD) — o console do painel
    ///     consome este stream no circuito Blazor sem disputar o DbContext scoped.
    /// </summary>
    public async IAsyncEnumerable<string> StreamLogsAsync(
        string containerId, bool follow = true, string tail = "200",
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var stream = await Client.Containers.GetContainerLogsAsync(containerId, false,
            new ContainerLogsParameters { ShowStdout = true, ShowStderr = true, Follow = follow, Tail = tail }, ct);

        var buffer = new byte[8192];
        while (!ct.IsCancellationRequested)
        {
            var read = await stream.ReadOutputAsync(buffer, 0, buffer.Length, ct);
            if (read.EOF) break;
            if (read.Count > 0) yield return Encoding.UTF8.GetString(buffer, 0, read.Count);
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────────

    // Carrega a instância + modpack (necessário para loader/versão no create). Rastreada (escrita de status).
    private async Task<ServerInstanceEntity> LoadAsync(Guid instanceId, CancellationToken ct)
    {
        return await db.ServerInstances
                   .Include(i => i.Modpack)
                   .FirstOrDefaultAsync(i => i.Id == instanceId, ct)
               ?? throw new InvalidOperationException("Instância não encontrada.");
    }

    // Procura um container pelo nome exato (a API filtra por substring; conferimos o nome com "/")
    private async Task<ContainerListResponse?> FindByNameAsync(string name, CancellationToken ct)
    {
        var list = await Client.Containers.ListContainersAsync(new ContainersListParameters
        {
            All = true,
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["name"] = new Dictionary<string, bool> { [name] = true }
            }
        }, ct);

        // Docker prefixa os nomes com "/"; casa o nome exato para não pegar um prefixo de outro
        return list.FirstOrDefault(c => c.Names.Any(n => n.TrimStart('/') == name));
    }

    private async Task SetStatusAsync(ServerInstanceEntity instance, ServerInstanceStatus status, CancellationToken ct)
    {
        instance.Status = status;
        instance.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}