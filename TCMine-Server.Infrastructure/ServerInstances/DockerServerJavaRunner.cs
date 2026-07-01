using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;
using TCMine_Application.Abstractions;

namespace TCMine_Server.Infrastructure.ServerInstances;

/// <summary>
/// Implementação Docker do <see cref="IServerJavaRunner"/>: roda <c>java</c> num container <b>efêmero</b>
/// (imagem só-Java do TCMine), com o diretório de trabalho montado como <c>/data</c>, aguarda o término
/// e captura a saída. Usado pelo provisionamento para rodar o instalador do loader
/// (<c>--installServer</c>) sem manter um processo Java no host.
///
/// Segue os logs <b>ao vivo</b> (<c>Follow=true</c>) para reportar o progresso do instalador linha a
/// linha, e ainda acumula a saída completa para o <see cref="JavaRunResult"/> (diagnóstico de falhas).
/// Não usa <c>AutoRemove</c>: o container é removido manualmente ao final (inclusive em erro).
/// </summary>
public sealed class DockerServerJavaRunner(DockerEnvironment docker) : IServerJavaRunner
{
    private const string WorkDir = "/data";

    // Rede de segurança: uma execução Java (ex.: instalar o NeoForge) que trave nunca deve prender o
    // provisionamento para sempre. Instalações reais levam poucos minutos; 30 min cobre até máquinas
    // lentas com folga. Ao estourar, o container é removido (finally) e o erro sobe claro.
    private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(30);

    public async Task<JavaRunResult> RunAsync(
        string workingDirectory, IReadOnlyList<string> arguments,
        IProgress<string>? output = null, string? containerName = null, CancellationToken ct = default)
    {
        var client = docker.Client;
        var hostPath = docker.ToHostPath(workingDirectory);

        await docker.EnsureImageAsync(docker.McImage, ct);

        // Nome fixo pedido → remove um eventual container órfão com o mesmo nome (o TCMine caiu no meio de
        // uma provisão anterior). Sem isso, o CreateContainer falharia com conflito de nome ao retomar.
        if (!string.IsNullOrEmpty(containerName))
            await RemoveByNameAsync(client, containerName, ct);

        var create = await client.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Image = docker.McImage,
            Name = containerName,
            // Entrypoint explícito = java: sobrepõe o "dotnet" da imagem do release (reuso de imagem)
            Entrypoint = ["java"],
            Cmd = [.. arguments],
            WorkingDir = WorkDir,
            HostConfig = new HostConfig
            {
                // Mounts (objeto Source/Target) em vez de Binds ("origem:destino"): o caminho do host
                // no Windows tem ':' no drive (P:\…), que colide com o separador do bind. O objeto evita
                // o parse ambíguo e monta o diretório certo.
                Mounts = [new Mount { Type = "bind", Source = hostPath, Target = WorkDir }],
                AutoRemove = false
            }
        }, ct);

        // Timeout ligado ao ct do chamador: cancela o stream/wait se a execução travar
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(RunTimeout);
        var runCt = timeoutCts.Token;

        try
        {
            await client.Containers.StartContainerAsync(create.ID, new ContainerStartParameters(), runCt);

            // Follow=true: o stream fica aberto e entrega os logs à medida que o instalador imprime;
            // encerra (EOF) quando o container sai. Assim damos progresso ao vivo em vez de esperar o fim.
            using var logs = await client.Containers.GetContainerLogsAsync(create.ID, false,
                new ContainerLogsParameters { ShowStdout = true, ShowStderr = true, Follow = true }, runCt);

            var combined = await StreamLinesAsync(logs, output, runCt);

            // O stream já terminou (container saiu) → o wait retorna o código na hora
            var wait = await client.Containers.WaitContainerAsync(create.ID, runCt);

            return new JavaRunResult((int)wait.StatusCode, combined);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            // Estouro do timeout (não um cancelamento do chamador) → mensagem clara; o finally remove o container
            throw new TimeoutException(
                $"A execução excedeu o limite de {RunTimeout.TotalMinutes:0} min e foi abortada (container removido). " +
                "Tente novamente; se persistir, verifique os recursos (CPU/memória) do Docker.");
        }
        finally
        {
            // Remove sempre (sucesso, falha ou timeout); Force cobre o caso de ainda estar de pé
            await client.Containers.RemoveContainerAsync(create.ID,
                new ContainerRemoveParameters { Force = true }, CancellationToken.None);
        }
    }

    // Remove (Force) qualquer container com o nome exato — limpa um órfão de uma execução interrompida
    private static async Task RemoveByNameAsync(DockerClient client, string name, CancellationToken ct)
    {
        var list = await client.Containers.ListContainersAsync(new ContainersListParameters
        {
            All = true,
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["name"] = new Dictionary<string, bool> { [name] = true }
            }
        }, ct);

        // O filtro é por substring; casa o nome exato (Docker prefixa com "/")
        foreach (var c in list.Where(c => c.Names.Any(n => n.TrimStart('/') == name)))
            await client.Containers.RemoveContainerAsync(c.ID,
                new ContainerRemoveParameters { Force = true }, ct);
    }

    // Lê o stream multiplexado (stdout+stderr) do container, acumula tudo e emite cada linha completa
    // pelo callback ao vivo. A saída do instalador é praticamente ASCII (paths/URLs), então decodificar
    // por bloco é seguro na prática.
    private static async Task<string> StreamLinesAsync(
        MultiplexedStream stream, IProgress<string>? output, CancellationToken ct)
    {
        var full = new StringBuilder();
        var carry = string.Empty; // linha parcial acumulada até chegar o '\n'
        var buffer = new byte[8192];

        while (true)
        {
            var read = await stream.ReadOutputAsync(buffer, 0, buffer.Length, ct);
            if (read.EOF) break;
            if (read.Count == 0) continue;

            var text = Encoding.UTF8.GetString(buffer, 0, read.Count);
            full.Append(text);

            if (output is null) continue; // sem consumidor → só acumula (evita trabalho de split)

            carry += text;
            int nl;
            while ((nl = carry.IndexOf('\n')) >= 0)
            {
                output.Report(carry[..nl].TrimEnd('\r'));
                carry = carry[(nl + 1)..];
            }
        }

        // Última linha sem '\n' final
        if (output is not null && carry.Length > 0)
            output.Report(carry.TrimEnd('\r'));

        return full.ToString();
    }
}
