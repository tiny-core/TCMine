namespace TCMine_Application.Contracts;

/// <summary>
///     Um conjunto de configs player-owned em disco no servidor: o par <c>(uuid, modpackId)</c> com o seu
///     tamanho, contagem de arquivos e último sync. Projeção para a tela admin de gestão de configs.
/// </summary>
public sealed record PlayerConfigSetDto(
    string Uuid,
    string ModpackId,
    string? ModpackName, // null = modpack já não existe na BD (pasta órfã)
    long SizeBytes,
    int FileCount,
    DateTimeOffset? UpdatedAt);

/// <summary>Visão geral das configs de jogador: o total em disco e todos os conjuntos (um por modpack).</summary>
public sealed record PlayerConfigOverviewDto(
    long TotalBytes,
    IReadOnlyList<PlayerConfigSetDto> Sets);