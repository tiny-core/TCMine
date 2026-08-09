using TCMine.Contracts.Modpacks;
using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Modpacks;

public sealed class Modpack : Entity, IOwnedEntity
{
    /// <summary>Identificador legível para URL, ex: "tecnologia-medieval".</summary>
    public required string Slug { get; set; }

    public required string Name { get; set; }
    public string? Summary { get; set; }

    /// <summary>Ícone no content store. Guardamos o hash, não a URL.</summary>
    public string? IconBlobSha256 { get; set; }

    public List<ModpackVersion> Versions { get; } = [];

    public required string MinecraftVersion { get; set; }
    public required ModLoader Loader { get; set; }
    public Guid OwnerId { get; set; }

    // ---------- Origem externa (pack importado) ----------

    /// <summary>
    ///     De onde este modpack foi importado (CurseForge, por ora). Nulo quando
    ///     foi criado do zero aqui.
    /// </summary>
    public ModFileOrigin? UpstreamProvider { get; set; }

    /// <summary>
    ///     Id do pack na origem. Identidade ESTÁVEL do pack lá fora — é por ela
    ///     que perguntamos "saiu versão nova?". Fica no Modpack, e não na versão,
    ///     porque não muda quando o autor publica uma atualização.
    /// </summary>
    public string? UpstreamProjectId { get; set; }

    /// <summary>Veio de um pack externo?</summary>
    public bool HasUpstream => UpstreamProvider is not null && UpstreamProjectId is not null;
}
