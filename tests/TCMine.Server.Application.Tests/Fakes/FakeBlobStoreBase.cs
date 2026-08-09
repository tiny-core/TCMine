using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Application.Tests.Fakes;

/// <summary>
///     Mesma ideia da <see cref="FakeModpackRepositoryBase" />: o teste sobrescreve
///     só o que exercita, e um método novo na porta não quebra fake nenhum.
/// </summary>
public abstract class FakeBlobStoreBase : IBlobStore
{
    public virtual Task<string> PutAsync(
        Stream content, string? expectedSha256, string contentType, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task<bool> ExistsAsync(string sha256, CancellationToken ct) => Task.FromResult(true);

    public virtual Task<Stream> OpenAsync(string sha256, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task<Uri?> TryGetDirectUrlAsync(string sha256, TimeSpan lifetime, CancellationToken ct) =>
        Task.FromResult<Uri?>(null);

    public virtual Task<string?> TryGetLocalPathAsync(string sha256, CancellationToken ct) =>
        Task.FromResult<string?>(null);
}
