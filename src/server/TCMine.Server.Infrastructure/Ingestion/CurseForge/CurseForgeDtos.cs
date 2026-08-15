using System.Text.Json.Serialization;

namespace TCMine.Server.Infrastructure.Ingestion.CurseForge;

// A API do CurseForge embrulha tudo num objeto { "data": ... }.
internal sealed record CurseForgeResponse<T>
{
    [JsonPropertyName("data")] public T? Data { get; init; }
}

internal sealed record CurseForgeMod
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("slug")] public string? Slug { get; init; }
    [JsonPropertyName("summary")] public string? Summary { get; init; }
    [JsonPropertyName("downloadCount")] public double DownloadCount { get; init; }
    [JsonPropertyName("logo")] public CurseForgeLogo? Logo { get; init; }

    /// <summary>
    ///     Falso quando o autor proibiu redistribuição por terceiros. Nesse caso
    ///     a API devolve downloadUrl nulo e o pack precisa do arquivo enviado à mão.
    /// </summary>
    [JsonPropertyName("allowModDistribution")]
    public bool? AllowModDistribution { get; init; }

    /// <summary>
    ///     Resumo das últimas releases por versão/loader. Vem de graça na busca —
    ///     é o que permite dizer se um mod serve ao pack sem uma chamada por
    ///     resultado.
    /// </summary>
    [JsonPropertyName("latestFilesIndexes")]
    public IReadOnlyList<CurseForgeFileIndex> LatestFilesIndexes { get; init; } = [];
}

internal sealed record CurseForgeFileIndex
{
    [JsonPropertyName("gameVersion")] public string? GameVersion { get; init; }

    /// <summary>Mesmos números do filtro: 1=Forge, 4=Fabric, 5=Quilt, 6=NeoForge.</summary>
    [JsonPropertyName("modLoader")]
    public int? ModLoader { get; init; }
}

internal sealed record CurseForgeLogo
{
    [JsonPropertyName("thumbnailUrl")] public string? ThumbnailUrl { get; init; }
    [JsonPropertyName("url")] public string? Url { get; init; }
}

internal sealed record CurseForgeFile
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("fileName")] public string? FileName { get; init; }

    /// <summary>Nulo quando a redistribuição foi negada pelo autor.</summary>
    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; init; }

    [JsonPropertyName("fileLength")] public long FileLength { get; init; }
    [JsonPropertyName("gameVersions")] public IReadOnlyList<string> GameVersions { get; init; } = [];
    [JsonPropertyName("hashes")] public IReadOnlyList<CurseForgeHash> Hashes { get; init; } = [];
    [JsonPropertyName("dependencies")] public IReadOnlyList<CurseForgeDependency> Dependencies { get; init; } = [];

    /// <summary>1 = release, 2 = beta, 3 = alpha.</summary>
    [JsonPropertyName("releaseType")]
    public int ReleaseType { get; init; }

    [JsonPropertyName("fileDate")] public DateTimeOffset FileDate { get; init; }
}

internal sealed record CurseForgeHash
{
    [JsonPropertyName("value")] public string? Value { get; init; }

    /// <summary>1 = SHA-1, 2 = MD5.</summary>
    [JsonPropertyName("algo")]
    public int Algo { get; init; }
}

internal sealed record CurseForgeDependency
{
    [JsonPropertyName("modId")] public int ModId { get; init; }

    /// <summary>1 = embedded, 2 = optional, 3 = required, 4 = tool, 5 = incompatible, 6 = include.</summary>
    [JsonPropertyName("relationType")]
    public int RelationType { get; init; }
}

/// <summary>Corpo do POST /v1/mods — consulta em lote de metadados.</summary>
internal sealed record CurseForgeModsRequest
{
    public required IReadOnlyList<int> ModIds { get; init; }
}
