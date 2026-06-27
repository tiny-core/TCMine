using Docker.DotNet.Models;
using TCMine_Application.Abstractions;

namespace TCMine_Infrastructure.ServerInstances;

/// <summary>
/// Implementação Docker do <see cref="IServerJavaRunner"/>: roda <c>java</c> num container <b>efêmero</b>
/// (imagem só-Java do TCMine), com o diretório de trabalho montado como <c>/data</c>, aguarda o término
/// e captura a saída. Usado pelo provisionamento para rodar o instalador do loader
/// (<c>--installServer</c>) sem manter um processo Java no host.
///
/// Não usa <c>AutoRemove</c>: precisamos ler os logs <i>depois</i> do exit para diagnosticar falhas;
/// o container é removido manualmente ao final (inclusive em erro).
/// </summary>
public sealed class DockerServerJavaRunner(DockerEnvironment docker) : IServerJavaRunner
{
    private const string WorkDir = "/data";

    public async Task<JavaRunResult> RunAsync(
        string workingDirectory, IReadOnlyList<string> arguments, CancellationToken ct = default)
    {
        var client = docker.Client;
        var hostPath = docker.ToHostPath(workingDirectory);

        await docker.EnsureImageAsync(docker.McImage, ct);

        var create = await client.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Image = docker.McImage,
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

        try
        {
            await client.Containers.StartContainerAsync(create.ID, new ContainerStartParameters(), ct);
            var wait = await client.Containers.WaitContainerAsync(create.ID, ct);

            // Logs completos após o término (stdout+stderr desmultiplexados)
            using var logs = await client.Containers.GetContainerLogsAsync(create.ID, false,
                new ContainerLogsParameters { ShowStdout = true, ShowStderr = true }, ct);
            var (stdout, stderr) = await logs.ReadOutputToEndAsync(ct);

            return new JavaRunResult((int)wait.StatusCode, stdout + stderr);
        }
        finally
        {
            // Remove sempre (sucesso ou falha); Force cobre o caso de ainda estar de pé
            await client.Containers.RemoveContainerAsync(create.ID,
                new ContainerRemoveParameters { Force = true }, CancellationToken.None);
        }
    }
}
