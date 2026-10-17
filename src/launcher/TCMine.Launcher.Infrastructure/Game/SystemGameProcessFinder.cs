using System.ComponentModel;
using System.Diagnostics;
using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Infrastructure.Game;

/// <summary>
///     Reencontra o processo do jogo pelo PID, conferindo a hora de início.
/// </summary>
public sealed class SystemGameProcessFinder : IGameProcessFinder
{
    /// <summary>
    ///     Folga na comparação da hora de início. A mesma API a deu nas duas
    ///     pontas, mas o arredondamento do sistema não é garantido; um segundo
    ///     ainda separa o nosso jogo de um programa que herdou o PID.
    /// </summary>
    private static readonly TimeSpan StartTolerance = TimeSpan.FromSeconds(1);

    public IGameProcess? TryReattach(GameProcessIdentity identity, string instanceDirectory)
    {
        Process process;

        try
        {
            process = Process.GetProcessById(identity.ProcessId);
        }
        catch (ArgumentException)
        {
            // Nenhum processo com esse PID: o jogo já saiu.
            return null;
        }

        try
        {
            var startedAt = new DateTimeOffset(process.StartTime);

            if (!process.HasExited && (startedAt - identity.StartedAt).Duration() <= StartTolerance)
                return new ReattachedGameProcess(process, identity, Path.Combine(instanceDirectory, "logs", "latest.log"));
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Saiu entre a busca e a consulta, ou o sistema recusou: não é nosso.
        }

        process.Dispose();
        return null;
    }
}
