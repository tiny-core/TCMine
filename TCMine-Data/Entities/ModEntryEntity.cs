using System.ComponentModel.DataAnnotations;
using TCMine_Core.modpack;

namespace TCMine_Data.Entities;

/// <summary>Um mod (CurseForge) pertencente a um modpack.</summary>
public class ModEntryEntity
{
    public int Id { get; set; }

    /// <summary>ID do mod no CurseForge (serializado como "modId" no manifesto público)</summary>
    public long CurseModId { get; set; }

    /// <summary>
    /// ID do arquivo (serializado como "fileId" no manifesto público) relacionado ao mod no CurseForge.
    /// Usado para identificar de forma única a versão específica do mod associada ao modpack.
    /// </summary>
    public long FileId { get; set; }

    [MaxLength(200)] public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Versão legível do arquivo (CurseForge DisplayName)
    /// </summary>
    [MaxLength(80)] public string? Version { get; set; }

    /// <summary>
    /// Nome do arquivo associado ao mod (serializado como "fileName" no manifesto público).
    /// </summary>
    [MaxLength(260)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// URL de origem no CurseForge — usada pelo servidor para baixar o jar uma vez.
    /// O launcher NÃO usa esta URL: baixa o jar do próprio servidor (ver project-modpack-mods-locais).
    /// </summary>
    [MaxLength(500)] public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// SHA-1 e tamanho do jar baixado — para o launcher verificar integridade e para dedup do cache.
    /// Preenchidos quando o servidor baixa o arquivo (o CF não fornece hash no CfFileRefDto).
    /// </summary>
    [MaxLength(40)] public string? Sha1 { get; set; }
    public long FileLength { get; set; }

    /// <summary>
    /// Destino no cliente: "mod", "resourcepack" ou "shaderpack"
    /// </summary>
    [MaxLength(20)] public string Target { get; set; } = "mod";

    /// <summary>
    /// Lado em que o mod roda — filtra o que vai para o cliente (launcher) vs servidor.
    /// Regra compartilhada em TCMine.Core.modpack.ModSideRules.
    /// </summary>
    public ModSide Side { get; set; } = ModSide.Both;

    /// <summary>
    /// Identificador único do modpack ao qual o mod pertence (referência para <see cref="ModpackEntity"/>).
    /// </summary>
    public Guid ModpackId { get; set; }

    /// <summary>
    /// O modpack ao qual este mod pertence, representado por uma instância de <see cref="ModpackEntity"/>.
    /// Relacionamento de chave estrangeira entre mods e modpacks. Apagar um modpack resultará na exclusão em cascata
    /// de todos os mods associados.
    /// </summary>
    public ModpackEntity? Modpack { get; set; }
}