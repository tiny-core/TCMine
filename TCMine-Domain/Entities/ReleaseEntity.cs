using System.ComponentModel.DataAnnotations;

namespace TCMine_Domain.Entities;

/// <summary>
///     Metadados de uma release do launcher. Os artefacts (Setup.exe, .nupkg,
///     releases.&lt;channel&gt;.json) ficam no diretório de updates e são servidos
///     pelo Velopack em <c>/updates</c>; esta entidade guarda o histórico/changelog.
/// </summary>
public class ReleaseEntity
{
    public int Id { get; set; }

    [MaxLength(40)] public string Version { get; set; } = string.Empty;

    [MaxLength(20)] public string Channel { get; set; } = "win";

    [MaxLength(4000)] public string Notes { get; set; } = string.Empty;

    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

    // Nomes dos arquivos carregados para o diretório de updates (separados por '\n')
    [MaxLength(4000)] public string Files { get; set; } = string.Empty;
}