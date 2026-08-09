using System.Text.Json.Serialization;

namespace TCMine.Server.Infrastructure.Ingestion.Modrinth;

/// <summary>
///     O <c>modrinth.index.json</c> de dentro de um .mrpack.
///     Bem mais rico que o manifest do CurseForge: cada arquivo já vem com o
///     hash, a URL de download e — o que mais importa aqui — o ambiente
///     (cliente/servidor) declarado pelo autor do pack.
/// </summary>
internal sealed record ModrinthPackIndex
{
    [JsonPropertyName("formatVersion")] public int FormatVersion { get; init; }

    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>Rótulo da versão dado pelo autor, ex.: "1.6.0".</summary>
    [JsonPropertyName("versionId")]
    public string? VersionId { get; init; }

    [JsonPropertyName("summary")] public string? Summary { get; init; }

    [JsonPropertyName("files")] public IReadOnlyList<ModrinthPackFile> Files { get; init; } = [];

    /// <summary>
    ///     Chaves como "minecraft", "neoforge", "fabric-loader", "forge",
    ///     "quilt-loader" → versão. É daqui que sai o loader do modpack.
    /// </summary>
    [JsonPropertyName("dependencies")]
    public IReadOnlyDictionary<string, string> Dependencies { get; init; } =
        new Dictionary<string, string>();
}

internal sealed record ModrinthPackFile
{
    /// <summary>Caminho relativo à instância, ex.: "mods/jei.jar".</summary>
    [JsonPropertyName("path")]
    public string Path { get; init; } = "";

    [JsonPropertyName("hashes")] public ModrinthPackHashes? Hashes { get; init; }

    /// <summary>
    ///     URLs diretas no CDN do Modrinth. Diferente do CurseForge, o pack já
    ///     entrega por onde baixar — não há intermediação nem opt-out de
    ///     redistribuição, porque o Modrinth só aceita licenças que a permitem.
    /// </summary>
    [JsonPropertyName("downloads")]
    public IReadOnlyList<string> Downloads { get; init; } = [];

    [JsonPropertyName("fileSize")] public long FileSize { get; init; }

    [JsonPropertyName("env")] public ModrinthPackEnv? Env { get; init; }
}

internal sealed record ModrinthPackHashes
{
    [JsonPropertyName("sha1")] public string? Sha1 { get; init; }
    [JsonPropertyName("sha512")] public string? Sha512 { get; init; }
}

/// <summary>Valores: "required", "optional", "unsupported".</summary>
internal sealed record ModrinthPackEnv
{
    [JsonPropertyName("client")] public string? Client { get; init; }
    [JsonPropertyName("server")] public string? Server { get; init; }
}

/// <summary>Uma versão publicada de um projeto (usada para achar o .mrpack).</summary>
internal sealed record ModrinthPackVersion
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("version_number")] public string? VersionNumber { get; init; }

    /// <summary>"release" | "beta" | "alpha".</summary>
    [JsonPropertyName("version_type")]
    public string? VersionType { get; init; }

    [JsonPropertyName("date_published")] public DateTimeOffset DatePublished { get; init; }
    [JsonPropertyName("files")] public IReadOnlyList<ModrinthPackVersionFile> Files { get; init; } = [];
}

internal sealed record ModrinthPackVersionFile
{
    [JsonPropertyName("url")] public string? Url { get; init; }
    [JsonPropertyName("filename")] public string? Filename { get; init; }
    [JsonPropertyName("primary")] public bool Primary { get; init; }
}

/// <summary>Resposta do /v2/search filtrada por project_type:modpack.</summary>
internal sealed record ModrinthPackSearchResponse
{
    [JsonPropertyName("hits")] public IReadOnlyList<ModrinthPackHit> Hits { get; init; } = [];
}

internal sealed record ModrinthPackHit
{
    [JsonPropertyName("project_id")] public string? ProjectId { get; init; }
    [JsonPropertyName("slug")] public string? Slug { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("icon_url")] public string? IconUrl { get; init; }
    [JsonPropertyName("author")] public string? Author { get; init; }
}
