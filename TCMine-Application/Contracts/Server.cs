using TCMine_Domain.Modpack;

namespace TCMine_Application.Contracts;

/// <summary>
///     Item do feed público de novidades (consumido pelo launcher). <c>ModpackId</c>/<c>ModpackName</c>
///     nulos = notícia <b>global</b> (do servidor); preenchidos = notícia daquele modpack.
/// </summary>
public sealed record NewsItemDto(
    string Tag,
    string Title,
    string Summary,
    DateTime PublishedAt,
    Guid? ModpackId,
    string? ModpackName);

/// <summary>
///     Representa um resumo de um modpack, incluindo metadados essenciais e estatísticas.
/// </summary>
public record ModpackSummaryDto(
    Guid Id,
    string Name,
    string Version,
    string Minecraft,
    ModLoader Loader,
    string LoaderVersion,
    string Description,
    int ModCount,
    int ServerCount,
    DateTime UpdatedAt,
    string? CurseForgeUrl = null);

/// <summary>
///     Representa um servidor associado a um modpack, contendo informações básicas para conexão.
/// </summary>
public record ServerDto(string Name, string Address, int Port);