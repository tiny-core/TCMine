using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace TCMine.Server.Infrastructure.Versions;

public sealed class MinecraftVersionSource(HttpClient http)
{
    private const string ManifestUrl =
        "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json";

    public async Task<IReadOnlyList<string>> GetAsync(bool releasesOnly, CancellationToken ct)
    {
        var manifest = await http.GetFromJsonAsync<Manifest>(ManifestUrl, ct);
        if (manifest is null)
            return [];

        // O manifesto já vem do mais novo para o mais antigo.
        return
        [
            .. manifest.Versions
                .Where(v => !releasesOnly || v.Type == "release")
                .Select(v => v.Id)
        ];
    }

    private sealed record Manifest(
        [property: JsonPropertyName("versions")]
        IReadOnlyList<Entry> Versions);

    private sealed record Entry(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("type")] string Type); // release | snapshot | old_beta | old_alpha
}