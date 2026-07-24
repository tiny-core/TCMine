using TCMine.Contracts.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Web.Mapping;

/// <summary>
///     Traduz entidades de domínio para DTOs.
///     Num lugar só: se um campo sensível não deve vazar, a decisão fica aqui e
///     não espalhada por cada endpoint. E quando o DTO mudar, o compilador
///     aponta este arquivo em vez de deixar um mapeamento defasado passar.
/// </summary>
public static class ModpackMappings
{
    public static ModpackDto ToDto(this Modpack modpack)
    {
        return new ModpackDto
        {
            Id = modpack.Id,
            Slug = modpack.Slug,
            Name = modpack.Name,
            Summary = modpack.Summary,
            IconUrl = null // preenchido quando houver endpoint de ícone
        };
    }

    public static ModpackVersionDto ToDto(this ModpackVersion version)
    {
        return new ModpackVersionDto
        {
            Id = version.Id,
            ModpackId = version.ModpackId,
            Version = version.Version,
            MinecraftVersion = version.MinecraftVersion,
            Loader = version.Loader,
            LoaderVersion = version.LoaderVersion,
            State = version.State,
            PublishedAt = version.PublishedAt ?? default,
            RecommendedMemoryMb = version.RecommendedMemoryMb,
            Files =
            [
                .. version.Files
                    // Server-only nunca vai ao cliente: ele não precisa e seria banda
                    // desperdiçada. O filtro do lado do launcher é uma segunda linha;
                    // esta é a primeira.
                    .Where(f => f.Side is not FileSide.ServerOnly)
                    .Select(f => new ModpackFileDto
                    {
                        Path = f.Path,
                        Sha256 = f.Sha256,
                        SizeBytes = f.SizeBytes,
                        Side = f.Side,
                        Optional = f.Optional
                    })
            ]
        };
    }
}