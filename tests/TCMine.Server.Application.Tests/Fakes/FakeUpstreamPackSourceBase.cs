using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Fakes;

/// <summary>Base para fakes de origem externa — ver <see cref="FakeModpackRepositoryBase" />.</summary>
public abstract class FakeUpstreamPackSourceBase : IUpstreamPackSource
{
    public virtual ModFileOrigin Origin => ModFileOrigin.CurseForge;

    public virtual ValueTask<bool> IsAvailableAsync(CancellationToken ct) => ValueTask.FromResult(true);

    public virtual Task<IReadOnlyList<UpstreamPackSummary>> SearchPacksAsync(
        string text, int limit, CancellationToken ct) => throw new NotImplementedException();

    public virtual Task<UpstreamPack?> FetchAsync(string projectId, string? fileId, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task<UpstreamRelease?> GetLatestReleaseAsync(string projectId, CancellationToken ct) =>
        throw new NotImplementedException();
}
