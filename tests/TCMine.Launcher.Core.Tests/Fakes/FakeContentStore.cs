using System.Text;
using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Core.Tests.Fakes;

/// <summary>Content store falso, em memória.</summary>
public sealed class FakeContentStore : IContentStore
{
    /// <summary>Hashes já presentes antes da instalação.</summary>
    public HashSet<string> Hashes { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Hashes acrescentados durante a instalação, na ordem.</summary>
    public List<string> Added { get; } = [];

    /// <summary>Caminho de destino → se foi pedido hardlink.</summary>
    public Dictionary<string, bool> Materialized { get; } = [];

    public Task<bool> ContainsAsync(string sha256, CancellationToken ct) =>
        Task.FromResult(Hashes.Contains(sha256));

    public async Task AddAsync(string sha256, Stream content, CancellationToken ct)
    {
        using var leitor = new StreamReader(content, Encoding.UTF8);
        await leitor.ReadToEndAsync(ct);

        Hashes.Add(sha256);
        Added.Add(sha256);
    }

    public Task<IReadOnlySet<string>> ListHashesAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlySet<string>>(Hashes);

    public Task MaterializeAsync(string sha256, string destinationPath, bool allowHardLink, CancellationToken ct)
    {
        Materialized[destinationPath] = allowHardLink;
        return Task.CompletedTask;
    }

    public Task<long> GetSizeOnDiskAsync(CancellationToken ct) => Task.FromResult(0L);
}
