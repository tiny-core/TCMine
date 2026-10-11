using Microsoft.Extensions.Caching.Memory;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Infrastructure.Versions;

public sealed class VersionCatalog(
    MinecraftVersionSource minecraft,
    NeoForgeVersionSource neoforge,
    ForgeVersionSource forge,
    FabricVersionSource fabric,
    QuiltVersionSource quilt,
    IMemoryCache cache) : IVersionCatalog
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    public Task<VersionLookup> GetMinecraftVersionsAsync(bool releasesOnly, CancellationToken ct) =>
        Cached($"mc:{releasesOnly}", () => minecraft.GetAsync(releasesOnly, ct), ct);

    public Task<VersionLookup> GetLoaderVersionsAsync(
        ModLoader loader, string minecraftVersion, bool releasesOnly, CancellationToken ct)
    {
        return Cached($"loader:{loader}:{minecraftVersion}:{releasesOnly}", () => loader switch
        {
            ModLoader.NeoForge => neoforge.GetAsync(minecraftVersion, releasesOnly, ct),
            ModLoader.Forge => forge.GetAsync(minecraftVersion, releasesOnly, ct),
            ModLoader.Fabric => fabric.GetAsync(minecraftVersion, releasesOnly, ct),
            ModLoader.Quilt => quilt.GetAsync(minecraftVersion, releasesOnly, ct),
            ModLoader.Vanilla => Task.FromResult<IReadOnlyList<string>>([]),
            _ => Task.FromResult<IReadOnlyList<string>>([])
        }, ct);
    }

    /// <summary>
    ///     Só o ACERTO vai para o cache. Guardar a falha fazia uma queda de
    ///     segundos na fonte virar meia hora de "não há versões" no painel — e,
    ///     pior, uma lista vazia em cache era indistinguível de uma resposta.
    ///     Uma lista vazia que a fonte DEU é resposta e fica em cache: "não há
    ///     Fabric para este Minecraft" é um fato.
    /// </summary>
    private async Task<VersionLookup> Cached(
        string key, Func<Task<IReadOnlyList<string>>> factory, CancellationToken ct)
    {
        if (cache.TryGetValue(key, out IReadOnlyList<string>? hit) && hit is not null)
            return VersionLookup.Of(hit);

        try
        {
            var value = await factory();
            cache.Set(key, value, Ttl);
            return VersionLookup.Of(value);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Rede fora, fonte instável ou resposta num formato novo: não se
            // sabe. Quem chamou decide o que fazer com "não sei" — a tela avisa,
            // a validação deixa passar.
            return VersionLookup.Unavailable;
        }
    }
}
