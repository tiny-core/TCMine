using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Microsoft.Extensions.Caching.Memory;
using TCMine_Application.Contracts;
using TCMine_Domain.Modpack;

namespace TCMine_Infrastructure.Minecraft;

/// <summary>
/// Busca listas de versões oficiais para preencher os seletores do editor de modpack:
/// versões do Minecraft (manifesto da Mojang) e versões de cada loader (NeoForge/Forge/Fabric/Quilt),
/// cada uma do seu endpoint oficial. Resultados são cacheados em memória (mudam raramente) e cada
/// busca é defensiva: falha de rede devolve lista vazia (o seletor cai para texto livre).
/// </summary>
public sealed class MinecraftVersionService(IHttpClientFactory http, IMemoryCache cache)
{
    // Versões mudam pouco; 1h de cache evita martelar os endpoints a cada abertura do editor
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    // ── Minecraft ────────────────────────────────────────────────────────────────────────────────

    /// <summary>Versões do Minecraft (mais novas primeiro), cada uma marcando se é release.</summary>
    public Task<IReadOnlyList<VersionOptionDto>> GetMinecraftVersionsAsync(CancellationToken ct = default)
    {
        return GetOrAddAsync("mc-versions", async token =>
        {
            var client = http.CreateClient();
            await using var stream = await client.GetStreamAsync(
                "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json", token);
            var manifest = await JsonSerializer.DeserializeAsync<MojangManifest>(stream, cancellationToken: token);

            // O manifesto já vem ordenado do mais novo para o mais antigo
            return (manifest?.Versions ?? [])
                .Select(v => new VersionOptionDto(v.Id, v.Type == "release"))
                .ToList();
        }, ct);
    }

    // ── Loaders ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Versões do loader escolhido, filtradas pela versão de Minecraft quando o loader é específico
    /// por versão (NeoForge/Forge). Fabric/Quilt são independentes da versão MC.
    /// </summary>
    public Task<IReadOnlyList<VersionOptionDto>> GetLoaderVersionsAsync(
        ModLoader loader, string? minecraft, CancellationToken ct = default)
    {
        return loader switch
        {
            ModLoader.Fabric => GetFabricLikeAsync("fabric", "https://meta.fabricmc.net/v2/versions/loader", ct),
            ModLoader.Quilt => GetFabricLikeAsync("quilt", "https://meta.quiltmc.org/v3/versions/loader", ct),
            ModLoader.NeoForge => GetNeoForgeAsync(minecraft, ct),
            ModLoader.Forge => GetForgeAsync(minecraft, ct),
            _ => Task.FromResult<IReadOnlyList<VersionOptionDto>>([])
        };
    }

    /// <summary>
    /// Fabric e Quilt expõem o mesmo formato: lista de <c>{ version, stable? }</c>
    /// </summary>
    private Task<IReadOnlyList<VersionOptionDto>> GetFabricLikeAsync(string key, string url, CancellationToken ct)
    {
        return GetOrAddAsync($"loader-{key}", async token =>
        {
            var client = http.CreateClient();
            await using var stream = await client.GetStreamAsync(url, token);
            var items = await JsonSerializer.DeserializeAsync<List<FabricLoader>>(stream, cancellationToken: token);

            return (items ?? [])
                // Sem "stable" explícito (Quilt), inferimos pelo sufixo de pré-lançamento
                .Select(i => new VersionOptionDto(i.Version,
                    i.Stable ?? !(i.Version.Contains("-beta") || i.Version.Contains("-pre") ||
                                  i.Version.Contains("-rc"))))
                .ToList();
        }, ct);
    }

    // NeoForge: a versão codifica o MC (1.X.Y → prefixo "X.Y.")
    private async Task<IReadOnlyList<VersionOptionDto>> GetNeoForgeAsync(string? minecraft, CancellationToken ct)
    {
        var all = await GetOrAddAsync("loader-neoforge", async token =>
        {
            var client = http.CreateClient();
            await using var stream = await client.GetStreamAsync(
                "https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge", token);
            var doc = await JsonSerializer.DeserializeAsync<NeoForgeVersions>(stream, cancellationToken: token);

            // Mais novas primeiro; "-beta" = pré-lançamento
            return (doc?.Versions ?? [])
                .AsEnumerable().Reverse()
                .Select(v => new VersionOptionDto(v, !v.Contains("-beta")))
                .ToList();
        }, ct);

        return FilterByPrefix(all, NeoForgePrefix(minecraft));
    }

    // Forge: maven-metadata.xml lista versões "MC-FORGE" (ex.: "1.21.1-52.0.63")
    private async Task<IReadOnlyList<VersionOptionDto>> GetForgeAsync(string? minecraft, CancellationToken ct)
    {
        var all = await GetOrAddAsync("loader-forge", async token =>
        {
            var client = http.CreateClient();
            await using var stream = await client.GetStreamAsync(
                "https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml", token);
            var xml = await XDocument.LoadAsync(stream, LoadOptions.None, token);

            return xml.Descendants("version")
                .Select(v => v.Value)
                .Reverse() // mais novas primeiro
                .Select(v => new VersionOptionDto(v, true)) // Forge não marca beta no maven
                .ToList();
        }, ct);

        return FilterForge(all, minecraft);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────

    // Prefixo NeoForge para uma versão MC "1.X[.Y]" → "X.Y." (Y=0 quando ausente)
    private static string? NeoForgePrefix(string? minecraft)
    {
        if (string.IsNullOrWhiteSpace(minecraft)) return null;
        var parts = minecraft.Split('.');
        if (parts.Length < 2 || parts[0] != "1") return null;
        var minor = parts.Length >= 3 ? parts[2] : "0";
        return $"{parts[1]}.{minor}.";
    }

    private static IReadOnlyList<VersionOptionDto> FilterByPrefix(IReadOnlyList<VersionOptionDto> all, string? prefix)
    {
        return prefix is null ? all : all.Where(v => v.Version.StartsWith(prefix, StringComparison.Ordinal)).ToList();
    }

    // Forge: filtra pelo prefixo "MC-" e tira o prefixo, deixando só a versão do Forge
    private static IReadOnlyList<VersionOptionDto> FilterForge(IReadOnlyList<VersionOptionDto> all, string? minecraft)
    {
        if (string.IsNullOrWhiteSpace(minecraft)) return all;
        var prefix = minecraft + "-";
        return all
            .Where(v => v.Version.StartsWith(prefix, StringComparison.Ordinal))
            .Select(v => v with { Version = v.Version[prefix.Length..] })
            .ToList();
    }

    // Cache assíncrono com TTL; em erro devolve lista vazia (não cacheia o erro)
    private async Task<IReadOnlyList<VersionOptionDto>> GetOrAddAsync(
        string key, Func<CancellationToken, Task<List<VersionOptionDto>>> factory, CancellationToken ct)
    {
        if (cache.TryGetValue(key, out IReadOnlyList<VersionOptionDto>? cached) && cached is not null)
            return cached;

        try
        {
            var result = await factory(ct);
            cache.Set<IReadOnlyList<VersionOptionDto>>(key, result, CacheTtl);
            return result;
        }
        catch
        {
            // Falha de rede/parse → seletor cai para texto livre (não trava o editor)
            return [];
        }
    }

    // ── DTOs de desserialização ────────────────────────────────────────────────────────────────────

    private sealed class MojangManifest
    {
        [JsonPropertyName("versions")] public List<MojangVersion> Versions { get; set; } = [];
    }

    private sealed class MojangVersion
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    }

    private sealed class FabricLoader
    {
        [JsonPropertyName("version")] public string Version { get; set; } = string.Empty;
        [JsonPropertyName("stable")] public bool? Stable { get; set; }
    }

    private sealed class NeoForgeVersions
    {
        [JsonPropertyName("versions")] public List<string> Versions { get; set; } = [];
    }
}