using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace TCMine.Server.Infrastructure.Versions;

public class QuiltVersionSource(HttpClient http)
{
    // A meta do PRÓPRIO Quilt. Isto apontava para a do Fabric, e o painel
    // oferecia versões de loader que não existem no Quilt — o container subia
    // pedindo ao itzg um loader que ele não tem como baixar.
    // Por versão do jogo, como no Fabric: só os loaders que servem àquele
    // Minecraft, e lista vazia quando o Quilt não o suporta.
    private const string LoaderUrl = "https://meta.quiltmc.org/v3/versions/loader/";

    public async Task<IReadOnlyList<string>> GetAsync(string minecraftVersion, bool releasesOnly, CancellationToken ct)
    {
        var entries = await http.GetFromJsonAsync<IReadOnlyList<Entry>>(
            LoaderUrl + Uri.EscapeDataString(minecraftVersion.Trim()), ct);

        if (entries is null)
            return [];

        return
        [
            .. entries
                .Select(e => e.Loader?.Version)
                .Where(v => v is not null && (!releasesOnly || IsStable(v)))
                .Select(v => v!)
        ];
    }

    /// <summary>
    ///     A meta do Quilt não traz o campo <c>stable</c> que a do Fabric traz: o
    ///     pré-lançamento só se distingue pelo sufixo SemVer (<c>0.31.0-beta.4</c>).
    ///     Ler <c>stable</c> aqui devolveria falso para tudo, e "só estáveis"
    ///     viraria uma lista vazia.
    /// </summary>
    private static bool IsStable(string version) => !version.Contains('-', StringComparison.Ordinal);

    private sealed record Entry(
        [property: JsonPropertyName("loader")] Loader? Loader);

    private sealed record Loader(
        [property: JsonPropertyName("version")]
        string Version);
}
