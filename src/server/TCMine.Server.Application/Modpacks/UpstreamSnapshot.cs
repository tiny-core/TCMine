using System.Text.Json;
using System.Text.Json.Serialization;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Retrato do pack como veio da origem — a BASE do merge de três vias.
///     Guardado em JSON na versão. Sem ele não dá para distinguir "o autor
///     mudou este mod" de "o admin mudou este mod", e a atualização passaria por
///     cima do trabalho de quem customizou.
/// </summary>
public sealed record UpstreamSnapshot
{
    /// <summary>Mods do pack: slug (id do projeto) → release fixada pelo autor.</summary>
    [JsonPropertyName("mods")]
    public required IReadOnlyDictionary<string, string> Mods { get; init; }

    /// <summary>Overrides: caminho → SHA-256 do conteúdo como veio da origem.</summary>
    [JsonPropertyName("overrides")]
    public required IReadOnlyDictionary<string, string> Overrides { get; init; }

    /// <summary>
    ///     Nomes legíveis dos mods: id do projeto → nome. Opcional de propósito —
    ///     snapshots gravados antes disto simplesmente não têm, e o progresso cai
    ///     de volta para o id em vez de quebrar.
    /// </summary>
    [JsonPropertyName("names")]
    public IReadOnlyDictionary<string, string> Names { get; init; } =
        new Dictionary<string, string>();

    public string ToJson() => JsonSerializer.Serialize(this, SnapshotJsonContext.Default.UpstreamSnapshot);

    public static UpstreamSnapshot? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize(json, SnapshotJsonContext.Default.UpstreamSnapshot);
        }
        catch (JsonException)
        {
            // Snapshot corrompido: sem base, o merge não roda. Tratar como
            // ausente deixa a UI dizer "sem base para comparar" em vez de quebrar.
            return null;
        }
    }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(UpstreamSnapshot))]
internal sealed partial class SnapshotJsonContext : JsonSerializerContext;
