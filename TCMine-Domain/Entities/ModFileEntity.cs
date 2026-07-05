using System.ComponentModel.DataAnnotations;

namespace TCMine_Domain.Entities;

/// <summary>
///     Um arquivo de mod único, independente de modpack — a identidade vem do <see cref="FileId" />
///     (id do arquivo no CurseForge; para uploads manuais, um id negativo derivado do conteúdo). Os
///     metadados intrínsecos ao arquivo (nome, hash, tamanho, URL) vivem aqui **uma única vez**; o
///     vínculo com cada modpack — e os atributos por-modpack (<c>Side</c>/<c>Target</c>) — fica em
///     <see cref="ModpackModEntity" />. Assim o mesmo arquivo em N modpacks não duplica metadados (o jar
///     já era compartilhado no cache de disco). Ver wiki: decisions/mods-many-to-many.
/// </summary>
public class ModFileEntity
{
    /// <summary>
    ///     ID do arquivo no CurseForge (serializado como "fileId" no manifesto) — **chave primária**.
    ///     Para uploads manuais é um id negativo determinístico pelo conteúdo. Único e imutável.
    /// </summary>
    public long FileId { get; set; }

    /// <summary>ID do mod no CurseForge (serializado como "modId" no manifesto público).</summary>
    public long CurseModId { get; set; }

    [MaxLength(200)] public string Name { get; set; } = string.Empty;

    /// <summary>Versão legível do arquivo (CurseForge DisplayName).</summary>
    [MaxLength(80)]
    public string? Version { get; set; }

    /// <summary>Nome do arquivo (serializado como "fileName" no manifesto público).</summary>
    [MaxLength(260)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    ///     URL de origem no CurseForge — usada pelo servidor para baixar o jar uma vez. O launcher NÃO
    ///     usa esta URL: baixa o jar do próprio servidor (ver [[concepts/modpack-mods-locais]]).
    /// </summary>
    [MaxLength(500)]
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    ///     SHA-1 e tamanho do jar baixado — para o launcher verificar integridade e dedup do cache.
    /// </summary>
    [MaxLength(40)]
    public string? Sha1 { get; set; }

    public long FileLength { get; set; }

    /// <summary>
    ///     Marcador de **órfão**: preenchido (UTC) quando o arquivo deixa de estar vinculado a qualquer
    ///     modpack; <c>null</c> enquanto houver ao menos um vínculo. O jar permanece no cache de disco —
    ///     este marcador serve para identificar candidatos a limpeza (GC) e exibir na lista de mods.
    /// </summary>
    public DateTime? OrphanedAt { get; set; }

    /// <summary>Vínculos deste arquivo com os modpacks que o usam.</summary>
    public List<ModpackModEntity> ModpackLinks { get; set; } = [];
}