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
}