using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Core.Tests.Fakes;

/// <summary>
///     Um gestor de JREs que não toca no disco.
///     Guarda o que "está instalado" e o que foi pedido, que são as duas coisas
///     que os testes deste caminho verificam: qual major o arranque pediu, e
///     quais a limpeza apagou.
/// </summary>
public sealed class FakeJavaLocator : IJavaLocator
{
    /// <summary>O que está no disco, por major e tamanho.</summary>
    public Dictionary<int, long> Installed { get; } = [];

    /// <summary>O major que o último EnsureRuntimeAsync pediu.</summary>
    public int? Requested { get; private set; }

    public List<int> Removed { get; } = [];

    /// <summary>Atirado pelo EnsureRuntimeAsync, para o caminho de falha.</summary>
    public Exception? Error { get; init; }

    public Task<string> EnsureRuntimeAsync(int majorVersion, IProgress<double>? progress, CancellationToken ct)
    {
        Requested = majorVersion;

        return Error is not null
            ? Task.FromException<string>(Error)
            : Task.FromResult($"/runtimes/{majorVersion}/bin/java");
    }

    public Task<IReadOnlyList<InstalledRuntime>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<InstalledRuntime>>(
            [.. Installed.Select(p => new InstalledRuntime(p.Key, p.Value))]);

    public Task RemoveAsync(int majorVersion, CancellationToken ct)
    {
        Removed.Add(majorVersion);
        Installed.Remove(majorVersion);

        return Task.CompletedTask;
    }
}
