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

    /// <summary>Nome de arquivo por id de release. Vazio salvo se o teste encher.</summary>
    public Dictionary<string, string> FileNames { get; } = new(StringComparer.Ordinal);

    /// <summary>Server pack que o teste quer devolver. Nulo = não existe.</summary>
    public IServerPackReader? ServerPack { get; set; }

    public virtual Task<IReadOnlyDictionary<string, string>> GetFileNamesAsync(
        IReadOnlyList<string> fileIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<string, string>>(
            fileIds.Where(FileNames.ContainsKey).ToDictionary(id => id, id => FileNames[id], StringComparer.Ordinal));

    public virtual Task<IServerPackReader?> OpenServerPackAsync(
        string projectId, string serverPackFileId, CancellationToken ct) =>
        Task.FromResult(ServerPack);
}
