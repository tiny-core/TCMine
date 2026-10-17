using System.Diagnostics;
using System.Runtime.CompilerServices;
using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Infrastructure.Game;

/// <summary>
///     Um jogo que outro launcher abriu. A saída dele era um pipe para o processo
///     antigo e morreu com ele, então o log vem do arquivo que o próprio
///     Minecraft escreve, <c>logs/latest.log</c>, seguido como um <c>tail -f</c>
///     até o jogo sair.
/// </summary>
internal sealed class ReattachedGameProcess(Process process, GameProcessIdentity identity, string logPath)
    : IGameProcess
{
    /// <summary>
    ///     Quanto do fim do arquivo ler ao reencontrar. O console guarda mil
    ///     linhas; um log de horas de jogo tem dezenas de megabytes, e lê-lo
    ///     inteiro para descartar quase tudo seria segundos de disco à toa.
    /// </summary>
    private const int TailBytes = 256 * 1024;

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    public int ExitCode => process.ExitCode;

    public GameProcessIdentity? Identity => identity;

    public async IAsyncEnumerable<string> ReadOutputAsync([EnumeratorCancellation] CancellationToken ct)
    {
        yield return "[launcher] O launcher foi reaberto com o jogo a correr; o registro abaixo vem de logs/latest.log.";

        var exited = process.WaitForExitAsync(ct);
        FileStream? stream = null;
        StreamReader? reader = null;

        try
        {
            while (true)
            {
                // O arquivo pode ainda não existir (o jogo a arrancar) — tenta-se
                // de novo a cada volta.
                if (reader is null && File.Exists(logPath))
                {
                    // ReadWrite|Delete: o jogo continua a escrever nele, e abri-lo
                    // de outro jeito o impediria.
                    stream = new FileStream(logPath, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete);

                    if (stream.Length > TailBytes)
                    {
                        stream.Seek(-TailBytes, SeekOrigin.End);
                        reader = new StreamReader(stream);

                        // A primeira linha começa a meio: descartada.
                        await reader.ReadLineAsync(ct);
                    }
                    else
                    {
                        reader = new StreamReader(stream);
                    }
                }

                if (reader is not null)
                {
                    while (await reader.ReadLineAsync(ct) is { } line)
                        yield return line;
                }

                if (exited.IsCompleted)
                    break;

                await Task.WhenAny(exited, Task.Delay(PollInterval, ct));
            }

            // O que o jogo escreveu entre a última leitura e a saída — é onde
            // mora a causa de um crash.
            if (reader is not null)
            {
                while (await reader.ReadLineAsync(ct) is { } line)
                    yield return line;
            }
        }
        finally
        {
            reader?.Dispose();

            if (stream is not null)
                await stream.DisposeAsync();
        }
    }

    public void Kill()
    {
        try
        {
            // A árvore inteira, como no SystemGameProcess.
            if (!process.HasExited)
                process.Kill(true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or SystemException)
        {
            // Já tinha saído.
        }
    }

    public void Dispose() => process.Dispose();
}
