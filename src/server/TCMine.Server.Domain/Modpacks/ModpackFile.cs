using TCMine.Contracts.Modpacks;
using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Modpacks;

public sealed class ModpackFile : Entity
{
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