using System.ComponentModel.DataAnnotations;
using TCMine_Domain.Modpack;

namespace TCMine_Domain.Entities;

/// <summary>
/// Tabela de junção entre <see cref="ModpackEntity"/> e <see cref="ModFileEntity"/> (relação N:N).
/// Além de ligar as duas, guarda os atributos que são **decisão por-modpack** — o mesmo arquivo
/// pode rodar em lados diferentes em packs diferentes. Chave primária composta (ModpackId, FileId):
/// um arquivo aparece no máximo uma vez por modpack.
/// </summary>
public class ModpackModEntity
{
    public Guid ModpackId { get; set; }
    public ModpackEntity? Modpack { get; set; }

    public long FileId { get; set; }
    public ModFileEntity? ModFile { get; set; }

    /// <summary>Destino no cliente: "mod", "resourcepack" ou "shaderpack" (por-modpack).</summary>
    [MaxLength(20)]
    public string Target { get; set; } = "mod";

    /// <summary>
    /// Lado em que o mod roda neste modpack — filtra o que vai para cliente vs servidor.
    /// Regra compartilhada em <see cref="ModSideRules"/>. Por-modpack (não é do arquivo).
    /// </summary>
    public ModSide Side { get; set; } = ModSide.Both;

    /// <summary>Ordem de exibição dentro do modpack (preserva a ordem de inserção no editor).</summary>
    public int SortOrder { get; set; }
}
