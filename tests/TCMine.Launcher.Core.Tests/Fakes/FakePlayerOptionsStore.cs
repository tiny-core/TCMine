using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Sync;

namespace TCMine.Launcher.Core.Tests.Fakes;

/// <summary>O options.txt e o estado da sincronização de cada instância, em memória.</summary>
public sealed class FakePlayerOptionsStore : IPlayerOptionsStore
{
    public Dictionary<InstanceKey, LocalOptions> Files { get; } = [];

    public Dictionary<InstanceKey, OptionsSyncState> States { get; } = [];

    public Task<LocalOptions?> ReadAsync(InstanceKey key, CancellationToken ct) =>
        Task.FromResult(Files.GetValueOrDefault(key));

    public Task WriteAsync(InstanceKey key, string text, CancellationToken ct)
    {
        Files[key] = new LocalOptions(text, DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public Task<OptionsSyncState?> ReadStateAsync(InstanceKey key, CancellationToken ct) =>
        Task.FromResult(States.GetValueOrDefault(key));

    public Task WriteStateAsync(InstanceKey key, OptionsSyncState state, CancellationToken ct)
    {
        States[key] = state;
        return Task.CompletedTask;
    }
}

/// <summary>As preferências deste PC, em memória.</summary>
public sealed class FakeLauncherPreferences(bool syncAll = false) : ILauncherPreferences
{
    public LauncherPreferences Current { get; private set; } = new() { SyncAllOptions = syncAll };

    public Task<LauncherPreferences> ReadAsync(CancellationToken ct) => Task.FromResult(Current);

    public Task WriteAsync(LauncherPreferences preferences, CancellationToken ct)
    {
        Current = preferences;
        return Task.CompletedTask;
    }
}
