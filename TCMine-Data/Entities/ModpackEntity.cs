using System.ComponentModel.DataAnnotations;
using TCMine_Core.modpack;

namespace TCMine_Data.Entities;

/// <summary>Um modpack oficial: metadados + mods + servidores. O <see cref="Id"/> é o slug público.</summary>
public class ModpackEntity
{
    public Guid Id { get; set; }
    [MaxLength(120)] public string Name { get; set; } = string.Empty;
    [MaxLength(40)] public string Version { get; set; } = string.Empty;
    [MaxLength(40)] public string Minecraft { get; set; } = string.Empty;

    /// <summary>
    /// Carregador de mods do modpack (NeoForge/Forge/Fabric/Quilt). O projeto não fica
    /// preso ao NeoForge — guardado como texto via conversão no AppDbContext.
    /// </summary>
    public ModLoader Loader { get; set; } = ModLoader.NeoForge;

    /// <summary>
    /// Versão do carregador (ex.: "21.1.77"); separada do tipo p/ exibir e instalar.
    /// </summary>
    [MaxLength(40)] public string LoaderVersion { get; set; } = string.Empty;

    /// <summary>
    /// A descrição textual do modpack, fornecendo informações adicionais sobre seu propósito,
    /// características ou outros detalhes que o tornem único. Limitado a 2000 caracteres.
    /// </summary>
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Indica se o modpack está publicamente disponível e visível.
    /// Controla o estado de publicação do modpack para os usuários.
    /// </summary>
    public bool IsPublished { get; set; } = true;

    /// <summary>
    /// RAM recomendada (MB) para este modpack; aplicada à instância no install
    /// </summary>
    public int? RecommendedRamMb { get; set; }

    /// <summary>
    /// Tem um bundle de overrides (configs/resourcepacks/options) guardado
    /// </summary>
    public bool HasOverrides { get; set; }

    /// <summary>
    /// Última modificação (UTC). Exposto no resumo para sync incremental do launcher
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Lista de mods associados a um modpack. Contém entradas que representam cada mod
    /// individual, incluindo informações como ID do CurseForge, a versão do mod e a URL de download.
    /// A exclusão de um modpack resulta na exclusão em cascata de todos os seus mods no banco de dados.
    /// </summary>
    public List<ModEntryEntity> Mods { get; set; } = [];

    /// <summary>
    /// Lista de servidores associados ao modpack. Estes servidores são anunciados no arquivo
    /// servers.dat pelo launcher, permitindo que usuários conectem diretamente aos servidores
    /// recomendados pelo modpack.
    /// </summary>
    public List<ServerEntryEntity> Servers { get; set; } = [];
}