using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Sync;

namespace TCMine.Launcher.Core.Tests.Fakes;

/// <summary>Pasta de instância falsa, em memória.</summary>
public sealed class FakeInstanceStore : IInstanceStore
{
    public Dictionary<InstanceKey, InstanceManifest> Manifests { get; } = [];

    public List<string> Deleted { get; } = [];

    public List<InstanceKey> Removed { get; } = [];

    public string PathFor(InstanceKey key) => Path.Combine("/instancias", key.ToDirectoryName());

    public Task<InstanceManifest?> ReadManifestAsync(InstanceKey key, CancellationToken ct) =>
        Task.FromResult(Manifests.GetValueOrDefault(key));

    public Task WriteManifestAsync(InstanceKey key, InstanceManifest manifest, CancellationToken ct)
    {
        Manifests[key] = manifest;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<InstalledInstance>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<InstalledInstance>>(
        [
            .. Manifests.Select(p => new InstalledInstance(p.Key, p.Value, 0, PathFor(p.Key)))
        ]);

    public Task DeleteFilesAsync(InstanceKey key, IEnumerable<string> relativePaths, CancellationToken ct)
    {
        Deleted.AddRange(relativePaths);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(InstanceKey key, CancellationToken ct)
    {
        Removed.Add(key);
        Manifests.Remove(key);

        return Task.CompletedTask;
    }
}
