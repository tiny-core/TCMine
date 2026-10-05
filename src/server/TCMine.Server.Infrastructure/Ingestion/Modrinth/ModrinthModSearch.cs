using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Infrastructure.Ingestion.Modrinth;

/// <summary>
///     Busca no endpoint /v2/search do Modrinth, filtrando por tipo (mod), versão
///     do Minecraft e loader do pack. Devolve o id do projeto como identidade estável.
/// </summary>
public sealed partial class ModrinthModSearch(
    HttpClient http,
    ILogger<ModrinthModSearch> logger) : IModSearch
{
    private static readonly string[] FacetsValue = ["project_type:mod"];
    private readonly ILogger<ModrinthModSearch> _logger = logger;

    public ModFileOrigin Origin => ModFileOrigin.Modrinth;

    // Não depende de chave nenhuma: está sempre à disposição.
    public ValueTask<bool> IsAvailableAsync(CancellationToken ct) => ValueTask.FromResult(true);

    public async Task<IReadOnlyList<ModSearchResult>> SearchAsync(ModSearchQuery query, CancellationToken ct)
    {
        // Só a faceta de tipo. Filtrar a BUSCA por versão e loader devolvia
        // lista vazia para mods que existem e ainda não saíram para a versão do
        // pack — e "nenhum mod encontrado" faz o admin achar que digitou errado.
        // A compatibilidade vira marcação no resultado, não filtro.
        var facets = JsonSerializer.Serialize(new[] { FacetsValue });

        var loader = ToModrinthLoader(query.Loader);

        var url = $"/v2/search?query={Uri.EscapeDataString(query.Text)}"
                  + $"&facets={Uri.EscapeDataString(facets)}"
                  + "&index=relevance"
                  + $"&limit={query.Limit}";

        try
        {
            var response = await http.GetFromJsonAsync<SearchResponse>(url, ct);
            if (response is null)
                return [];

            return
            [
                .. response.Hits
                    .Select(h => new ModSearchResult(
                        // O id, não o slug: é a identidade que o pack importado
                        // e as dependências usam. Com o slug, o mesmo mod vindo
                        // pelos dois caminhos virava dois .jar em mods/ — e o
                        // slug ainda pode ser renomeado pelo autor.
                        h.ProjectId,
                        h.Title,
                        h.Description,
                        h.IconUrl,
                        h.Downloads,
                        Serve(h, query.MinecraftVersion, loader),
                        VersoesRecentes(h)))
            ];
        }
        catch (HttpRequestException ex)
        {
            LogSearchError(ex, query.Text);
            return [];
        }
    }

    public async Task<IReadOnlyList<UpstreamRelease>> ListVersionsAsync(
        string projectId, string minecraftVersion, ModLoader loader, CancellationToken ct)
    {
        // Mesmo filtro que a ingestão usa ao resolver: o que aparece aqui é
        // exatamente o que ela aceitaria instalar.
        var url = $"/v2/project/{Uri.EscapeDataString(projectId)}/version"
                  + $"?game_versions={Uri.EscapeDataString($"[\"{minecraftVersion}\"]")}"
                  + $"&loaders={Uri.EscapeDataString($"[\"{ToModrinthLoader(loader)}\"]")}";

        try
        {
            var versions = await http.GetFromJsonAsync(
                url, ModrinthJsonContext.Default.IReadOnlyListModrinthVersion, ct);

            return versions is null
                ? []
                : versions
                    .Select(v => new UpstreamRelease(
                        v.Id,
                        v.VersionNumber,
                        v.DatePublished,
                        v.VersionType is null or "release",
                        string.Join(", ", v.GameVersions)))
                    .ToArray();
        }
        catch (HttpRequestException ex)
        {
            LogSearchError(ex, projectId);
            return [];
        }
    }

    // O nome do loader no domínio difere do que o Modrinth espera nas categorias.
    private static string ToModrinthLoader(ModLoader loader)
    {
        return loader switch
        {
            ModLoader.Forge => "forge",
            ModLoader.NeoForge => "neoforge",
            ModLoader.Fabric => "fabric",
            ModLoader.Quilt => "quilt",
            ModLoader.Vanilla => "minecraft",
            _ => throw new ArgumentOutOfRangeException(nameof(loader), loader, null)
        };
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao buscar '{Query}' no Modrinth.")]
    private partial void LogSearchError(Exception ex, string query);

    private sealed record SearchResponse(
        [property: JsonPropertyName("hits")] IReadOnlyList<Hit> Hits);

    /// <summary>Tem release para a versão e o loader do pack?</summary>
    private static bool Serve(Hit hit, string minecraftVersion, string loader)
    {
        // Sem informação não se acusa incompatibilidade.
        if (hit.Versions.Count is 0)
            return true;

        var versaoServe = hit.Versions.Contains(minecraftVersion, StringComparer.OrdinalIgnoreCase);

        // O loader vem nas categorias, junto com temas ("technology", "magic").
        var loaderServe = hit.Categories.Count is 0
                          || hit.Categories.Contains(loader, StringComparer.OrdinalIgnoreCase);

        return versaoServe && loaderServe;
    }

    private static string? VersoesRecentes(Hit hit)
    {
        // As últimas da lista são as mais novas na resposta do Modrinth.
        var versions = hit.Versions.TakeLast(4).Reverse().ToList();
        return versions.Count is 0 ? null : string.Join(", ", versions);
    }

    private sealed record Hit(
        [property: JsonPropertyName("project_id")]
        string ProjectId,
        [property: JsonPropertyName("slug")] string? Slug,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("description")]
        string Description,
        [property: JsonPropertyName("icon_url")]
        string? IconUrl,
        [property: JsonPropertyName("downloads")]
        int Downloads,
        [property: JsonPropertyName("versions")]
        IReadOnlyList<string> Versions,
        [property: JsonPropertyName("categories")]
        IReadOnlyList<string> Categories);
}
