using TCMine_Core.modpack;

namespace TCMine_Core.Contracts;

/// <summary>
/// Representa uma notícia com metadados associados.
/// </summary>
public record NewsDto(string Tag, string Title, string Data, string Summary);

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
    DateTime UpdatedAt);

/// <summary>
/// Representa um servidor associado a um modpack, contendo informações básicas para conexão.
/// </summary>
public record ServerDto(string Name, string Address, int Port);

/// <summary>
/// Representa uma versão publicada de software, incluindo metadados como versão, notas de lançamento,
/// canal de distribuição e data de publicação.
/// </summary>
public record ReleaseDto(string Version, string Notes, string Channel, DateTime PublishedAt);