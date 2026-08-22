using TCMine.Contracts.Modpacks;
using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Modpacks;

public sealed class ModpackFile : Entity
{
    /// <summary>
    ///     Caminho relativo à raiz da instância. Mil e vinte e quatro cobre
    ///     folgadamente os caminhos profundos que packs grandes trazem em
    ///     config/ e kubejs/ — o limite anterior, de 512, era atingido por packs
    ///     reais do CurseForge.
    /// </summary>
    public const int MaxPathLength = 1024;

    /// <summary>
    ///     Prefixo do slug sintético de um override.
    ///     O slug de override é este prefixo mais o caminho, o que o torna,
    ///     POR DEFINIÇÃO, maior que o caminho. Quando os dois campos tinham o
    ///     mesmo limite, um caminho que cabia gerava um slug que não cabia — e
    ///     o erro chegava do banco, sobre uma coluna que ninguém tinha
    ///     configurado à mão.
    /// </summary>
    public const string OverrideSlugPrefix = "override:";

    /// <summary>
    ///     Derivado, e não um número solto: é o que impede os dois limites de
    ///     divergirem de novo se um deles mudar.
    /// </summary>
    public const int MaxProjectSlugLength = MaxPathLength + 32;

    /// <summary>Slug sintético de um override, a partir do caminho.</summary>
    public static string OverrideSlug(string path) => OverrideSlugPrefix + path;

    public required Guid ModpackVersionId { get; set; }

    /// <summary>
    ///     Identidade estável do mod, independente da versão do arquivo.
    ///     Para mods resolvidos, é o slug do projeto (Modrinth) ou o id do
    ///     projeto (CurseForge). Para uploads manuais, o admin informa ou
    ///     derivamos do nome. É por este campo que sabemos que jei-1.2.jar e
    ///     jei-1.5.jar são o MESMO mod em versões diferentes — e que só um deles
    ///     pode existir na pasta ao mesmo tempo.
    /// </summary>
    public string? ProjectSlug { get; set; }

    /// <summary>Caminho relativo à raiz da instância, ex: "mods/jei.jar".</summary>
    public required string Path { get; set; }

    /// <summary>Chave no content store.</summary>
    public required string Sha256 { get; set; }

    public required long SizeBytes { get; set; }
    public required FileSide Side { get; set; }
    public bool Optional { get; set; }

    /// <summary>
    ///     De onde veio. Serve só para auditoria e para re-resolver depois —
    ///     esta informação nunca é enviada ao launcher, que trabalha só com hash.
    /// </summary>
    public ModFileOrigin Origin { get; set; } = ModFileOrigin.ManualUpload;

    /// <summary>ID do projeto/arquivo na origem, quando houver.</summary>
    public string? OriginReference { get; set; }

    /// <summary>
    ///     URL do ícone do mod na origem (ex.: Modrinth), quando houver. Puramente
    ///     cosmético — exibido na grade de mods do painel. Nunca vai ao launcher.
    /// </summary>
    public string? IconUrl { get; set; }
}

public enum ModFileOrigin
{
    /// <summary>Enviado pelo admin pela UI. Sempre disponível como escape.</summary>
    ManualUpload,

    /// <summary>Preferido: sem API key e a licença sempre permite redistribuição.</summary>
    Modrinth,

    CurseForge,

    /// <summary>Veio da pasta overrides do pack (config, script).</summary>
    Override
}
