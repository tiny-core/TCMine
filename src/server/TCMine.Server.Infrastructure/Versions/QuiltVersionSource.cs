using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace TCMine.Server.Infrastructure.Versions;

public class QuiltVersionSource(HttpClient http)
{
    // A lista de loader do Fabric é independente da versão do MC.
    private const string LoaderUrl = "https://meta.fabricmc.net/v2/versions/loader";

    public async Task<IReadOnlyList<string>> GetAsync(bool releasesOnly, CancellationToken ct)
    {
        var loaders = await http.GetFromJsonAsync<IReadOnlyList<Entry>>(LoaderUrl, ct);
        if (loaders is null)
            return [];

        return
        [
            .. loaders
                .Where(l => !releasesOnly || l.Stable)
                .Select(l => l.Version)
        ];
    }

    private sealed record Entry(
        [property: JsonPropertyName("version")]
        string Version,
        [property: JsonPropertyName("stable")] bool Stable);
}