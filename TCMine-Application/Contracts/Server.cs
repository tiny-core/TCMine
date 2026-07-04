using TCMine_Domain.Modpack;

namespace TCMine_Application.Contracts;

/// <summary>
/// Representa uma notícia com metadados associados.
/// </summary>
public record NewsDto(string Tag, string Title, string Data, string Summary);

/// <summary>
/// Item do feed público de novidades (consumido pelo launcher). <c>ModpackId</c>/<c>ModpackName</c>
/// nulos = notícia <b>global</b> (do servidor); preenchidos = notícia daquele modpack.
/// </summary>
public sealed record NewsItemDto(
    string Tag,
    string Title,
    string Summary,
    DateTime PublishedAt,
    Guid? ModpackId,
    string? ModpackName);

/// <summary>
/// Representa um resumo de um modpack, incluindo metadados essenciais e estatísticas.
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
/// Representa um servidor associado a um modpack, contendo informações básicas para conexão.
/// </summary>
public record ServerDto(string Name, string Address, int Port);

/// <summary>
/// Representa uma versão publicada de software, incluindo metadados como versão, notas de lançamento,
/// canal de distribuição e data de publicação.
/// </summary>
public record ReleaseDto(string Version, string Notes, string Channel, DateTime PublishedAt);