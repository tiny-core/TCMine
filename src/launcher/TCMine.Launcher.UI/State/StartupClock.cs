using System.Diagnostics;

namespace TCMine.Launcher.UI.State;

/// <summary>
///     TEMPORÁRIO — linha de base da refatoração (docs/BASELINE.md).
///     Removido no fim da fase 8.
///     Conta a partir do início do PROCESSO, e não de um Stopwatch criado no
///     App: assim o número inclui a carga do runtime, que é justamente o que
///     pesa numa abertura a frio.
/// </summary>
public static class StartupClock
{
    private static readonly DateTime ProcessStart = ReadProcessStart();

    public static long ElapsedMs =>
        (long)(DateTime.UtcNow - ProcessStart).TotalMilliseconds;

    private static DateTime ReadProcessStart()
    {
        using var process = Process.GetCurrentProcess();
        return process.StartTime.ToUniversalTime();
    }
}
