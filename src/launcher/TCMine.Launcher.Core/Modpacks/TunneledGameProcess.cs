using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Core.Modpacks;

/// <summary>
///     O processo do jogo com o túnel preso a ele: o túnel vive enquanto o jogo
///     vive. Decorador, e não um campo a mais em quem guarda o processo, porque
///     quem guarda (a sessão do jogo, a tela) não precisa saber que há túnel —
///     e esquecer de fechá-lo deixaria uma porta local aberta até o launcher
///     fechar.
/// </summary>
public sealed class TunneledGameProcess(IGameProcess inner, IGameTunnelSession tunnel) : IGameProcess
{
    private int _closed;

    public int ExitCode => inner.ExitCode;

    public async IAsyncEnumerable<string> ReadOutputAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        try
        {
            await foreach (var line in inner.ReadOutputAsync(ct))
                yield return line;
        }
        finally
        {
            // A saída termina quando o processo sai: é a hora de fechar.
            await CloseTunnelAsync();
        }
    }

    public void Kill() => inner.Kill();

    public void Dispose()
    {
        inner.Dispose();

        // Dispose é síncrono na interface; o fechamento do túnel é rápido (só
        // para de aceitar e corta as conexões), e esperar aqui evita deixá-lo
        // para trás num caminho em que ninguém leu a saída.
        CloseTunnelAsync().AsTask().GetAwaiter().GetResult();
    }

    private ValueTask CloseTunnelAsync() =>
        Interlocked.Exchange(ref _closed, 1) == 0 ? tunnel.DisposeAsync() : ValueTask.CompletedTask;
}
