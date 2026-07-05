using System.ComponentModel.DataAnnotations;
using TCMine_Domain.Modpack;

namespace TCMine_Domain.Entities;

/// <summary>
///     Modelo **plano** de um mod em um modpack — usado como rascunho do editor e como item de
///     import. Junta os campos do arquivo (<see cref="ModFileEntity" />) com os atributos por-modpack
///     (<c>Side</c>/<c>Target</c>) numa única estrutura conveniente para a UI. **Não** é uma entidade EF:
///     a persistência decompõe isto em <see cref="ModFileEntity" /> (compartilhado) + <see cref="ModpackModEntity" />
///     (junção). Ver wiki: decisions/mods-many-to-many.
/// </summary>
public class ModEntryEntity
{
    /// <summary>ID do mod no CurseForge (serializado como "modId" no manifesto público).</summary>
    public long CurseModId { get; set; }

    /// <summary>ID do arquivo no CurseForge (serializado como "fileId"); identidade do arquivo.</summary>
    public long FileId { get; set; }

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

    /// <summary>SHA-1 e tamanho do jar — para o launcher verificar integridade e dedup do cache.</summary>
    [MaxLength(40)]
    public string? Sha1 { get; set; }

    public long FileLength { get; set; }

    /// <summary>Destino no cliente: "mod", "resourcepack" ou "shaderpack" (por-modpack).</summary>
    [MaxLength(20)]
    public string Target { get; set; } = "mod";

    /// <summary>
    ///     Lado em que o mod roda — filtra o que vai para o cliente (launcher) vs servidor.
    ///     Regra compartilhada em <see cref="ModSideRules" />. Por-modpack.
    /// </summary>
    public ModSide Side { get; set; } = ModSide.Both;
}