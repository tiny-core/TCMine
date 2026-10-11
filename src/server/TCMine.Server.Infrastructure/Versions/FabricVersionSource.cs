using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace TCMine.Server.Infrastructure.Versions;

public sealed class FabricVersionSource(HttpClient http)
{
    // Por versão do jogo, e não a lista geral: a geral traz todo loader que já
    // existiu, inclusive para um Minecraft que o Fabric não suporta. Aqui a
    // meta devolve só os loaders que têm intermediário para aquele jogo — e
    // uma lista vazia quando não há nenhum, que é a resposta certa.
    private const string LoaderUrl = "https://meta.fabricmc.net/v2/versions/loader/";

    public async Task<IReadOnlyList<string>> GetAsync(string minecraftVersion, bool releasesOnly, CancellationToken ct)
    {
        var entries = await http.GetFromJsonAsync<IReadOnlyList<Entry>>(
            LoaderUrl + Uri.EscapeDataString(minecraftVersion.Trim()), ct);

        if (entries is null)
            return [];

        return
        [
            .. entries
                .Select(e => e.Loader)
                .Where(l => l is not null && (!releasesOnly || l.Stable))
                .Select(l => l!.Version)
        ];
    }

    private sealed record Entry(
        [property: JsonPropertyName("loader")] Loader? Loader);

    private sealed record Loader(
        [property: JsonPropertyName("version")]
        string Version,
        [property: JsonPropertyName("stable")] bool Stable);
}
