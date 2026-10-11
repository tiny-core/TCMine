using Microsoft.Extensions.Logging.Abstractions;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;

namespace TCMine.Server.Application.Tests.Fakes;

/// <summary>
///     Catálogo de versões sem rede: devolve uma lista fixa de builds de loader,
///     ou "não sei" quando <see cref="Failed" /> está ligado.
/// </summary>
public sealed class FakeVersionCatalog(params string[] loaderVersions) : IVersionCatalog
{
    public bool Failed { get; init; }

    public Task<VersionLookup> GetMinecraftVersionsAsync(bool releasesOnly, CancellationToken ct) =>
        Task.FromResult(VersionLookup.Of([]));

    public Task<VersionLookup> GetLoaderVersionsAsync(
        ModLoader loader, string minecraftVersion, bool releasesOnly, CancellationToken ct) =>
        Task.FromResult(Failed ? VersionLookup.Unavailable : VersionLookup.Of(loaderVersions));

    /// <summary>A conferência de loader sobre este catálogo.</summary>
    public LoaderVersionGuard Guard() => new(this, NullLogger<LoaderVersionGuard>.Instance);

    /// <summary>
    ///     Para os testes que não são sobre o loader: o catálogo "fora do ar" deixa
    ///     qualquer build passar, que é o comportamento real nessa situação.
    /// </summary>
    public static LoaderVersionGuard PermissiveGuard() => new FakeVersionCatalog { Failed = true }.Guard();
}
