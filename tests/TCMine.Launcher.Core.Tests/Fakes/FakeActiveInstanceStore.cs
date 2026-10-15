using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Sync;

namespace TCMine.Launcher.Core.Tests.Fakes;

/// <summary>A escolha da instância ativa, em memória.</summary>
public sealed class FakeActiveInstanceStore : IActiveInstanceStore
{
    public InstanceKey? Chosen { get; private set; }

    public Task<InstanceKey?> ReadAsync(CancellationToken ct) => Task.FromResult(Chosen);

    public Task WriteAsync(InstanceKey key, CancellationToken ct)
    {
        Chosen = key;
        return Task.CompletedTask;
    }
}
