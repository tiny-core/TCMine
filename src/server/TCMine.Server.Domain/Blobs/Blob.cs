namespace TCMine.Server.Domain.Blobs;

/// <summary>
///     Arquivo endereçado por conteúdo: o SHA-256 é a identidade.
///     Consequência prática: deduplicação sai de graça. Subir a versão 1.6 de um
///     pack costuma trocar 5 mods de 200 — os outros 195 já estão no disco e
///     nem são baixados de novo.
///     Não herda de Entity porque a chave primária é o hash, não um Guid.
/// </summary>
public sealed class Blob
{
    public required string Sha256 { get; set; }
    public required long SizeBytes { get; set; }
    public required string ContentType { get; set; }

    /// <summary>Onde os bytes estão. Permite política híbrida disco/object storage.</summary>
    public required BlobLocation Location { get; set; }

    /// <summary>Caminho no disco ou key no bucket.</summary>
    public required string StorageKey { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Para uma futura limpeza de blobs órfãos.</summary>
    public DateTimeOffset? LastAccessedAt { get; set; }
}

public enum BlobLocation
{
    LocalDisk,
    ObjectStorage
}