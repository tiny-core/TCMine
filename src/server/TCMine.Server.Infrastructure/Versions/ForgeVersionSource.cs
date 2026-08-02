using System.Xml.Linq;

namespace TCMine.Server.Infrastructure.Versions;

public sealed class ForgeVersionSource(HttpClient http)
{
    private const string MetadataUrl =
        "https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml";

    public async Task<IReadOnlyList<string>> GetAsync(string minecraftVersion, bool releasesOnly, CancellationToken ct)
    {
        await using var stream = await http.GetStreamAsync(MetadataUrl, ct);
        var doc = await XDocument.LoadAsync(stream, LoadOptions.None, ct);

        // Forge codifica como "{mc}-{forge}", ex.: "1.21.1-52.0.63". Filtramos
        // pelo prefixo do MC e devolvemos só a parte do Forge.
        var prefix = $"{minecraftVersion}-";

        return
        [
            .. doc.Descendants("version")
                .Select(x => x.Value)
                .Where(v => v.StartsWith(prefix, StringComparison.Ordinal))
                .Select(v => v[prefix.Length..])
                .Reverse()
        ];
        // Nota: o Forge não marca beta no maven; o releasesOnly aqui é no-op.
        // Se quiseres "recommended vs latest", isso vem do promotions.json —
        // dá para acrescentar depois se precisares.
    }
}
