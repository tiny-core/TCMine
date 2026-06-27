using System.ComponentModel.DataAnnotations;
using TCMine_Domain.Modpack;

namespace TCMine_Domain.Entities;

/// <summary>
/// Índice de uma instalação de servidor (loader + Minecraft) já baixada e montada uma única vez,
/// compartilhada por todas as instâncias que usam a mesma tupla
/// (<see cref="Loader"/>, <see cref="LoaderVersion"/>, <see cref="MinecraftVersion"/>).
///
/// <para><b>Por que existe (economia de disco):</b> uma instalação de NeoForge/Forge traz um
/// <c>libraries/</c> pesado (centenas de MB) idêntico entre instâncias do mesmo loader+versão.
/// Em vez de cada instância baixar e guardar a sua cópia, o provisioner instala <b>uma vez</b> sob
/// <c>tcmine-data/server-cache/installed/{slug}/</c> e cada instância apenas aponta para lá
/// (symlink no Linux/Docker, hardlink/cópia no Windows de dev). Esta linha é o <b>índice</b> dessa
/// pasta: a verdade continua sendo o filesystem, mas o registro dá visibilidade no painel e
/// habilita GC por LRU (<see cref="LastUsedAt"/>) das instalações que nenhuma instância usa mais.</para>
///
/// <para>Espelha a política do cache de jars de mods (<see cref="ModFileEntity"/> +
/// <c>tcmine-data/mods</c>): conteúdo compartilhado no disco, dedup por identidade.</para>
/// </summary>
public class ServerRuntimeCacheEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Loader instalado (NeoForge/Forge/Fabric/Quilt) — guardado como texto via conversão no AppDbContext.
    /// Parte da chave lógica do cache (índice único com versão do loader + versão do Minecraft).
    /// </summary>
    public ModLoader Loader { get; set; }

    /// <summary>Versão do loader instalada (ex.: <c>"21.1.77"</c>). Vazia para Vanilla.</summary>
    [MaxLength(40)]
    public string LoaderVersion { get; set; } = string.Empty;

    /// <summary>Versão do Minecraft instalada (ex.: <c>"1.21.1"</c>).</summary>
    [MaxLength(40)]
    public string MinecraftVersion { get; set; } = string.Empty;

    /// <summary>
    /// Caminho da instalação relativo à raiz do cache de runtime
    /// (<c>tcmine-data/server-cache/installed</c>) — ex.: <c>"neoforge-21.1.77-mc1.21.1"</c>.
    /// </summary>
    [MaxLength(200)]
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>Tamanho total em disco da instalação (bytes) — exibido no painel e usado para o GC.</summary>
    public long SizeBytes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Último uso (UTC): atualizado sempre que uma instância é provisionada contra este cache.
    /// O GC remove instalações não referenciadas por nenhuma instância e ociosas há mais tempo (LRU).
    /// </summary>
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
}
