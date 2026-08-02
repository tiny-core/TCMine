using System.Text.Json.Serialization;

namespace TCMine.Server.Infrastructure.Ingestion.Modrinth;

/// <summary>
///     Subconjunto da resposta da API do Modrinth que nos interessa.
///     Só mapeamos os campos usados — a resposta real tem dezenas. JsonPropertyName
///     porque a API usa snake_case e não queremos herdar essa convenção nos nossos
///     nomes.
/// </summary>
internal sealed record ModrinthVersion
{
    [JsonPropertyName("id")] public required string Id { get; init; }

    [JsonPropertyName("version_number")] public required string VersionNumber { get; init; }

    [JsonPropertyName("game_versions")] public required IReadOnlyList<string> GameVersions { get; init; }

    [JsonPropertyName("loaders")] public required IReadOnlyList<string> Loaders { get; init; }

    [JsonPropertyName("files")] public required IReadOnlyList<ModrinthFile> Files { get; init; }

    [JsonPropertyName("dependencies")] public IReadOnlyList<ModrinthDependency> Dependencies { get; init; } = [];
}

internal sealed record ModrinthFile
{
    [JsonPropertyName("url")] public required string Url { get; init; }

    [JsonPropertyName("filename")] public required string Filename { get; init; }

    [JsonPropertyName("size")] public required long Size { get; init; }

    [JsonPropertyName("hashes")] public required ModrinthHashes Hashes { get; init; }

    /// <summary>O Modrinth marca um arquivo como primário quando há vários.</summary>
    [JsonPropertyName("primary")]
    public bool Primary { get; init; }
}

internal sealed record ModrinthHashes
{
    [JsonPropertyName("sha512")] public string? Sha512 { get; init; }

    [JsonPropertyName("sha1")] public string? Sha1 { get; init; }
}

internal sealed class ModrinthDependency
{
    [JsonPropertyName("project_id")] public string? ProjectId { get; init; }

    [JsonPropertyName("version_id")] public string? VersionId { get; init; }

    [JsonPropertyName("dependency_type")] public string? DependencyType { get; init; }
}
