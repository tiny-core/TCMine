using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Infrastructure.Ingestion.Modrinth;

/// <summary>
///     Busca no endpoint /v2/search do Modrinth, filtrando por tipo (mod), versão
///     do Minecraft e loader do pack. Devolve o slug como identidade estável.
/// </summary>
public sealed partial class ModrinthModSearch(
    HttpClient http,
    ILogger<ModrinthModSearch> logger) : IModSearch
{
    private static readonly string[] FacetsValue = ["project_type:mod"];
    private readonly ILogger<ModrinthModSearch> _logger = logger;

    public async Task<IReadOnlyList<ModSearchResult>> SearchAsync(ModSearchQuery query, CancellationToken ct)
    {
        // Facetas do Modrinth: array de arrays, cada sub-array é um OR e o
        // conjunto é um AND. Aqui: só mods E compatível com a versão do MC E
        // com o loader do pack.
        var facets = JsonSerializer.Serialize(new[]
        {
            FacetsValue,
            [$"versions:{query.MinecraftVersion}"],
            [$"categories:{ToModrinthLoader(query.Loader)}"]
        });

        var url = $"/v2/search?query={Uri.EscapeDataString(query.Text)}"
                  + $"&facets={Uri.EscapeDataString(facets)}"
                  + $"&limit={query.Limit}";

        try
        {
            var response = await http.GetFromJsonAsync<SearchResponse>(url, ct);
            if (response is null)
                return [];

            return response.Hits
                .Select(h => new ModSearchResult(
                    h.Slug ?? h.ProjectId,
                    h.Title,
                    h.Description,
                    h.IconUrl,
                    h.Downloads))
                .ToList();
        }
        catch (HttpRequestException ex)
        {
            LogSearchError(ex, query.Text);
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
        int Downloads);
}