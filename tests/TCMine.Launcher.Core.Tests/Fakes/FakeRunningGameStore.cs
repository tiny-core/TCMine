using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Core.Tests.Fakes;

/// <summary>O registo do jogo aberto, em memória.</summary>
public sealed class FakeRunningGameStore : IRunningGameStore
{
    public RunningGameRecord? Record { get; set; }

    public int Clears { get; private set; }

    public Task<RunningGameRecord?> ReadAsync(CancellationToken ct) => Task.FromResult(Record);

    public Task WriteAsync(RunningGameRecord record, CancellationToken ct)
    {
        Record = record;
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken ct)
    {
        Record = null;
        Clears++;
        return Task.CompletedTask;
    }
}
