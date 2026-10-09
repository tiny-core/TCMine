using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace TCMine.Server.Infrastructure.Versions;

public class QuiltVersionSource(HttpClient http)
{
    // A meta do PRÓPRIO Quilt. Isto apontava para a do Fabric, e o painel
    // oferecia versões de loader que não existem no Quilt — o container subia
    // pedindo ao itzg um loader que ele não tem como baixar.
    // A lista de loader é independente da versão do MC.
    private const string LoaderUrl = "https://meta.quiltmc.org/v3/versions/loader";

    public async Task<IReadOnlyList<string>> GetAsync(bool releasesOnly, CancellationToken ct)
    {
        var loaders = await http.GetFromJsonAsync<IReadOnlyList<Entry>>(LoaderUrl, ct);
        if (loaders is null)
            return [];

        return
        [
            .. loaders
                .Select(l => l.Version)
                .Where(v => !releasesOnly || IsStable(v))
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
        [property: JsonPropertyName("version")]
        string Version);
}
