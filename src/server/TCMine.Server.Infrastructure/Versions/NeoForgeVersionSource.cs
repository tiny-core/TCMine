using System.Xml.Linq;

namespace TCMine.Server.Infrastructure.Versions;

public sealed class NeoForgeVersionSource(HttpClient http)
{
    private const string MetadataUrl =
        "https://maven.neoforged.net/releases/net/neoforged/neoforge/maven-metadata.xml";

    /// <summary>
    ///     O artefato antigo, só do Minecraft 1.20.1: o NeoForge nasceu como um
    ///     fork do Forge e manteve o nome e a numeração dele ("1.20.1-47.1.106")
    ///     até a 1.20.2, quando passou para o artefato e o esquema próprios.
    /// </summary>
    private const string LegacyMetadataUrl =
        "https://maven.neoforged.net/releases/net/neoforged/forge/maven-metadata.xml";

    private const string LegacyMinecraft = "1.20.1";

    public async Task<IReadOnlyList<string>> GetAsync(string minecraftVersion, bool releasesOnly, CancellationToken ct)
    {
        var minecraft = minecraftVersion.Trim();

        if (minecraft == LegacyMinecraft)
        {
            // "1.20.1-47.1.106" → "47.1.106": é a parte do NeoForge que o
            // container e o launcher recebem.
            var legacyPrefix = $"{LegacyMinecraft}-";

            return Newest(
                (await FetchVersionsAsync(LegacyMetadataUrl, ct))
                .Where(v => v.StartsWith(legacyPrefix, StringComparison.Ordinal))
                .Select(v => v[legacyPrefix.Length..]),
                releasesOnly);
        }

        // Esquema que não se reconhece (um snapshot, por exemplo): lista vazia.
        // Antes isto devolvia TUDO, e o painel oferecia builds de outras versões
        // do jogo como se servissem.
        if (ToNeoPrefix(minecraft) is not { } prefix)
            return [];

        return Newest(
            (await FetchVersionsAsync(MetadataUrl, ct)).Where(v => v.StartsWith(prefix, StringComparison.Ordinal)),
            releasesOnly);
    }

    /// <summary>
    ///     O começo das versões do NeoForge para um Minecraft. O NeoForge codifica
    ///     o jogo no próprio número, de dois jeitos:
    ///     série 1.x — tira o "1." e completa o patch: 1.21.1 → "21.1.", 1.21 → "21.0.";
    ///     numeração por ano (26 em diante) — usa a versão inteira e acrescenta a
    ///     build: 26.1 → "26.1.0.", 26.1.2 → "26.1.2.".
    ///     Nulo para o que não tem nenhuma das duas formas.
    /// </summary>
    public static string? ToNeoPrefix(string minecraftVersion)
    {
        var parts = minecraftVersion.Trim().Split('.');

        if (parts.Length is < 2 or > 3 || !parts.All(p => p.Length > 0 && p.All(char.IsAsciiDigit)))
            return null;

        var patch = parts.Length == 3 ? parts[2] : "0";

        return parts[0] == "1"
            ? $"{parts[1]}.{patch}."
            : $"{parts[0]}.{parts[1]}.{patch}.";
    }

    /// <summary>
    ///     Da mais nova para a mais antiga (o maven lista ao contrário). "Só
    ///     releases" tira tudo o que tem sufixo: -beta, -alpha e os snapshots.
    /// </summary>
    private static IReadOnlyList<string> Newest(IEnumerable<string> versions, bool releasesOnly) =>
    [
        .. versions
            .Where(v => !releasesOnly || !v.Contains('-', StringComparison.Ordinal))
            .Reverse()
    ];

    private async Task<IReadOnlyList<string>> FetchVersionsAsync(string url, CancellationToken ct)
    {
        await using var stream = await http.GetStreamAsync(url, ct);
        var doc = await XDocument.LoadAsync(stream, LoadOptions.None, ct);
        return [.. doc.Descendants("version").Select(x => x.Value)];
    }
}
