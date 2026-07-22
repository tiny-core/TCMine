namespace TCMine.Contracts.Modpacks;

public sealed record ModpackDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public required string Name { get; init; }
    public string? Summary { get; init; }
    public Uri? IconUrl { get; init; }
}

/// <summary>
///     Manifest RESOLVIDO de uma versão do modpack.
///     Repare que não há nenhuma referência a CurseForge ou Modrinth aqui. A
///     resolução acontece uma única vez, no publish, e a partir daí os arquivos
///     são servidos pelo próprio TCMine Server. Isso torna um pack publicado
///     imutável: nem mod despublicado, nem cota de API esgotada quebram alguém
///     que já estava jogando.
/// </summary>
public sealed record ModpackVersionDto
{
    public required Guid Id { get; init; }
    public required Guid ModpackId { get; init; }

    /// <summary>SemVer da versão do pack, ex: "1.4.0".</summary>
    public required string Version { get; init; }

    public required string MinecraftVersion { get; init; }
    public required ModLoader Loader { get; init; }
    public required string LoaderVersion { get; init; }

    public required ModpackVersionState State { get; init; }
    public required DateTimeOffset PublishedAt { get; init; }

    /// <summary>RAM recomendada em MB. Sugestão para a UI, não um limite.</summary>
    public int? RecommendedMemoryMb { get; init; }

    public required IReadOnlyList<ModpackFileDto> Files { get; init; }
}

/// <summary>
///     Um arquivo do pack.
///     O launcher resolve tudo por HASH, nunca por URL — a origem é detalhe de
///     implementação. Se o SHA-256 bate, o arquivo está correto, venha de onde vier.
/// </summary>
public sealed record ModpackFileDto
{
    /// <summary>Caminho relativo à raiz da instância, ex: "mods/jei.jar".</summary>
    public required string Path { get; init; }

    /// <summary>Hex minúsculo, 64 caracteres. É a chave do content store.</summary>
    public required string Sha256 { get; init; }

    public required long SizeBytes { get; init; }

    /// <summary>Onde este arquivo se aplica (equivale ao env do .mrpack).</summary>
    public required FileSide Side { get; init; }

    /// <summary>Opcional no cliente: shaders, resource pack extra.</summary>
    public bool Optional { get; init; }
}

public enum ModLoader
{
    Vanilla,
    Forge,
    NeoForge,
    Fabric,
    Quilt
}

public enum FileSide
{
    Both,
    ClientOnly,
    ServerOnly
}

public enum ModpackVersionState
{
    /// <summary>Criada, ainda sem resolução.</summary>
    Draft,

    /// <summary>Job em background baixando e hasheando os arquivos.</summary>
    Resolving,

    /// <summary>Pronta para uso. A partir daqui é imutável.</summary>
    Ready,

    /// <summary>Resolução falhou (ex: mod com redistribuição negada pelo autor).</summary>
    Failed,

    /// <summary>Não oferecida a novos clientes, mas mantida para quem já usa.</summary>
    Archived
}