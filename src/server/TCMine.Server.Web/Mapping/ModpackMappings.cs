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
            // A capa é um blob servido pelo endpoint genérico de blobs. Sem hash,
            // fica null e a UI cai no avatar com a inicial.
            IconUrl = modpack.IconBlobSha256 is { } sha
                ? new Uri($"/api/v1/blobs/{sha}", UriKind.Relative)
                : null,
            MinecraftVersion = modpack.MinecraftVersion,
            Loader = modpack.Loader
        };
    }

    /// <summary>Para listas: o que o seletor mostra, sem os arquivos.</summary>
    public static ModpackVersionSummaryDto ToSummaryDto(this ModpackVersion version) => new()
    {
        Id = version.Id,
        Version = version.Version,
        LoaderVersion = version.LoaderVersion,
        PublishedAt = version.PublishedAt ?? default,
        RecommendedMemoryMb = version.RecommendedMemoryMb
    };

    public static ModpackVersionDto ToDto(this ModpackVersion version)
    {
        return new ModpackVersionDto
        {
            Id = version.Id,
            ModpackId = version.ModpackId,
            Version = version.Version,
            LoaderVersion = version.LoaderVersion,
            State = version.State,
            PublishedAt = version.PublishedAt ?? default,
            RecommendedMemoryMb = version.RecommendedMemoryMb,
            // .ToArray(), NUNCA [.. x]: o alvo é IReadOnlyList<ModpackFileDto>, e uma
            // expressão de coleção contra essa interface materializa o tipo interno
            // sintetizado pelo compilador, que o MessagePack do launcher não
            // serializa — a chamada de Hub derruba a conexão em runtime (ver §8 do
            // CLAUDE.md; já aconteceu com GetModpacksAsync/GetServersAsync).
            Files = version.Files
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
                .ToArray()
        };
    }
}
