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

    public Task<IReadOnlyList<string>> GetMinecraftVersionsAsync(bool releasesOnly, CancellationToken ct) =>
        Cached($"mc:{releasesOnly}", () => minecraft.GetAsync(releasesOnly, ct));

    public Task<IReadOnlyList<string>> GetLoaderVersionsAsync(
        ModLoader loader, string minecraftVersion, bool releasesOnly, CancellationToken ct)
    {
        return Cached($"loader:{loader}:{minecraftVersion}:{releasesOnly}", () => loader switch
        {
            ModLoader.NeoForge => neoforge.GetAsync(minecraftVersion, releasesOnly, ct),
            ModLoader.Forge => forge.GetAsync(minecraftVersion, releasesOnly, ct),
            ModLoader.Fabric => fabric.GetAsync(releasesOnly, ct),
            ModLoader.Quilt => quilt.GetAsync(releasesOnly, ct),
            ModLoader.Vanilla => Task.FromResult<IReadOnlyList<string>>([]),
            _ => Task.FromResult<IReadOnlyList<string>>([])
        });
    }

    private async Task<IReadOnlyList<string>> Cached(string key, Func<Task<IReadOnlyList<string>>> factory)
    {
        if (cache.TryGetValue(key, out IReadOnlyList<string>? hit) && hit is not null)
            return hit;

        try
        {
            var value = await factory();
            cache.Set(key, value, Ttl);
            return value;
        }
        catch (Exception)
        {
            // Rede fora / fonte instável: devolve vazio em vez de rebentar o
            // formulário. O campo continua utilizável (o utilizador ainda pode
            // digitar, já que o domínio aceita texto livre).
            return [];
        }
    }
}
