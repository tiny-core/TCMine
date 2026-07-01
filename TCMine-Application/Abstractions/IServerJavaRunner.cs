namespace TCMine_Application.Abstractions;

/// <summary>
/// Resultado de uma execução Java efêmera: código de saída e a saída combinada (stdout+stderr)
/// capturada, para diagnóstico quando um install falha.
/// </summary>
public sealed record JavaRunResult(int ExitCode, string Output)
{
    public bool Success => ExitCode == 0;
}

/// <summary>
/// Porta para rodar um comando <c>java</c> até o fim num <b>container efêmero</b> e aguardar o exit
/// code. É a costura entre o provisionamento (que precisa rodar o instalador do loader, ex.:
/// <c>java -jar neoforge-installer.jar --installServer</c>) e a execução em Docker. A implementação
/// concreta (Docker-out-of-Docker via Docker.DotNet) entra no passo do <c>DockerMinecraftManager</c>;
/// o provisioner depende só desta interface.
///
/// Distinto do servidor em si (processo longo, gerenciado): aqui o container roda, faz o trabalho,
/// sai e é removido (<c>--rm</c>).
/// </summary>
public interface IServerJavaRunner
{
    /// <summary>
    /// Roda <c>java <paramref name="arguments"/></c> num container efêmero com
    /// <paramref name="workingDirectory"/> (caminho no host) montado como diretório de trabalho.
    /// Bloqueia até o processo terminar e devolve o resultado. Lança se o ambiente Docker não estiver
    /// disponível.
    /// </summary>
    /// <param name="workingDirectory">Diretório de trabalho no host, montado no container.</param>
    /// <param name="arguments">Argumentos passados ao executável <c>java</c> (sem o "java" inicial).</param>
    /// <param name="output">
    ///     Opcional: recebe a saída do processo <b>linha a linha, ao vivo</b> (enquanto roda), para
    ///     refletir o progresso do instalador (ex.: downloads do NeoForge) na UI. A saída completa
    ///     continua no <see cref="JavaRunResult.Output"/> ao final, para diagnóstico.
    /// </param>
    /// <param name="containerName">
    ///     Opcional: nome fixo do container efêmero (ex.: <c>tcmine-install-{slug}</c>). Dá um handle
    ///     estável — se sobrar um container com esse nome (o TCMine caiu no meio), ele é removido antes de
    ///     criar o novo, evitando conflito de nome ao **retomar** uma provisão.
    /// </param>
    /// <param name="ct">Token de cancelamento.</param>
    Task<JavaRunResult> RunAsync(
        string workingDirectory, IReadOnlyList<string> arguments,
        IProgress<string>? output = null, string? containerName = null, CancellationToken ct = default);
}
