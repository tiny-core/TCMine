using System.Text.Json.Serialization;

namespace TCMine.Server.Infrastructure.Ingestion.CurseForge;

/// <summary>
///     O manifest.json que vive na raiz do .zip de um modpack do CurseForge.
///     Só os campos que usamos.
/// </summary>
internal sealed record CurseForgeManifest
{
    [JsonPropertyName("minecraft")] public CurseForgeManifestMinecraft? Minecraft { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("version")] public string? Version { get; init; }
    [JsonPropertyName("author")] public string? Author { get; init; }
    [JsonPropertyName("files")] public IReadOnlyList<CurseForgeManifestFile> Files { get; init; } = [];

    /// <summary>Pasta com os arquivos avulsos. Por convenção "overrides".</summary>
    [JsonPropertyName("overrides")]
    public string? Overrides { get; init; }
}

internal sealed record CurseForgeManifestMinecraft
{
    [JsonPropertyName("version")] public string? Version { get; init; }

    [JsonPropertyName("modLoaders")]
    public IReadOnlyList<CurseForgeManifestLoader> ModLoaders { get; init; } = [];
}

internal sealed record CurseForgeManifestLoader
{
    /// <summary>Formato "neoforge-21.1.100" ou "forge-47.2.0".</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("primary")] public bool Primary { get; init; }
}

internal sealed record CurseForgeManifestFile
{
    [JsonPropertyName("projectID")] public int ProjectId { get; init; }
    [JsonPropertyName("fileID")] public int FileId { get; init; }
    [JsonPropertyName("required")] public bool Required { get; init; } = true;
}
