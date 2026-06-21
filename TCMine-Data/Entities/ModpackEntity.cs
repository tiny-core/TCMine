using System.ComponentModel.DataAnnotations;
using TCMine_Core.modpack;

namespace TCMine_Data.Entities;

/// <summary>Um modpack oficial: metadados + mods + servidores. O <see cref="Id"/> é o slug público.</summary>
public abstract class ModpackEntity
{
    [MaxLength(80)] public string Id { get; set; } = string.Empty;

    [MaxLength(120)] public string Name { get; set; } = string.Empty;

    [MaxLength(40)] public string Version { get; set; } = string.Empty;

    [MaxLength(40)] public string Minecraft { get; set; } = string.Empty;

    // Carregador de mods do modpack (NeoForge/Forge/Fabric/Quilt). O projeto não fica
    // preso ao NeoForge — guardado como texto via conversão no AppDbContext.
    public ModLoader Loader { get; set; } = ModLoader.NeoForge;

    // Versão do carregador (ex.: "21.1.77"); separada do tipo p/ exibir e instalar.
    [MaxLength(40)] public string LoaderVersion { get; set; } = string.Empty;

    [MaxLength(2000)] public string Description { get; set; } = string.Empty;

    public bool IsPublished { get; set; } = true;

    // RAM recomendada (MB) para este modpack; aplicada à instância no install
    public int? RecommendedRamMb { get; set; }

    // Tem um bundle de overrides (configs/resourcepacks/options) guardado
    public bool HasOverrides { get; set; }

    // Última modificação (UTC). Exposto no resumo para sync incremental do launcher
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<ModEntryEntity> Mods { get; set; } = [];
    public List<ServerEntryEntity> Servers { get; set; } = [];
}