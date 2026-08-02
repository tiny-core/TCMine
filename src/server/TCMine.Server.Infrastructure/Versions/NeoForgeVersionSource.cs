using System.Xml.Linq;

namespace TCMine.Server.Infrastructure.Versions;

public sealed class NeoForgeVersionSource(HttpClient http)
{
    private const string MetadataUrl =
        "https://maven.neoforged.net/releases/net/neoforged/neoforge/maven-metadata.xml";

    public async Task<IReadOnlyList<string>> GetAsync(string minecraftVersion, bool releasesOnly, CancellationToken ct)
    {
        var all = await FetchVersionsAsync(MetadataUrl, ct);

        // NeoForge codifica o MC no próprio número: MC 1.21.1 → loader 21.1.x.
        // MC "1.21.1" vira prefixo "21.1."; MC "1.21" vira "21.0.".
        var prefix = ToNeoPrefix(minecraftVersion);

        return
        [
            .. all
                .Where(v => prefix is null || v.StartsWith(prefix, StringComparison.Ordinal))
                .Where(v => !releasesOnly || !v.Contains("-beta", StringComparison.OrdinalIgnoreCase))
                .Reverse() // maven vem do mais antigo; queremos o mais novo primeiro
        ];
    }

    // "1.21.1" → "21.1."   |   "1.21" → "21.0."
    private static string? ToNeoPrefix(string mc)
    {
        var parts = mc.Split('.');
        if (parts.Length < 2 || parts[0] != "1")
            return null; // esquema desconhecido: não filtra, mostra tudo

        var minor = parts[1];
        var patch = parts.Length >= 3 ? parts[2] : "0";
        return $"{minor}.{patch}.";
    }

    private async Task<IReadOnlyList<string>> FetchVersionsAsync(string url, CancellationToken ct)
    {
        await using var stream = await http.GetStreamAsync(url, ct);
        var doc = await XDocument.LoadAsync(stream, LoadOptions.None, ct);
        return [.. doc.Descendants("version").Select(x => x.Value)];
    }
}
