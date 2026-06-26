using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TCMine_Domain.Entities;

/// <summary>
/// Origem CurseForge de um modpack importado (1:1 com <see cref="ModpackEntity"/>, PK = ModpackId).
/// Registra de qual projeto/arquivo CF o modpack foi importado e a versão aplicada, para depois
/// verificar atualizações e oferecer mesclar. Os campos <c>Latest*</c> + <c>LastCheckedAt</c> são um
/// **cache** da última checagem — evitam bater na API do CurseForge a cada visualização.
/// </summary>
public class ModpackImportSourceEntity
{
    /// <summary>PK e FK 1:1 para o modpack do TCMine.</summary>
    public Guid ModpackId { get; set; }
    public ModpackEntity? Modpack { get; set; }

    /// <summary>Id do projeto (modpack) no CurseForge.</summary>
    public long CurseProjectId { get; set; }

    [MaxLength(200)] public string CurseProjectName { get; set; } = string.Empty;

    /// <summary>Id do arquivo (.zip) do modpack CF aplicado agora (a "versão" instalada).</summary>
    public long InstalledFileId { get; set; }

    [MaxLength(120)] public string? InstalledVersion { get; set; }

    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    // ── Cache da última checagem de atualização (economia de API) ────────────────────────────────

    /// <summary>Quando foi a última checagem de atualização (null = nunca). Base do TTL.</summary>
    public DateTime? LastCheckedAt { get; set; }

    /// <summary>Id do arquivo mais recente conhecido no CF (cache); null = desconhecido.</summary>
    public long? LatestFileId { get; set; }

    [MaxLength(120)] public string? LatestVersion { get; set; }

    /// <summary>Há atualização? (latest conhecido difere do instalado). Não mapeado — derivado.</summary>
    [NotMapped]
    public bool UpdateAvailable => LatestFileId is { } latest && latest != InstalledFileId;
}
