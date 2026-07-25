using TCMine.Contracts.Modpacks;

namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Busca mods numa origem. Hoje só Modrinth; CurseForge entra depois como
///     segunda implementação, sem mexer no consumidor.
/// </summary>
public interface IModSearch
{
    Task<IReadOnlyList<ModSearchResult>> SearchAsync(ModSearchQuery query, CancellationToken ct);
}

public sealed record ModSearchQuery(
    string Text,
    string MinecraftVersion,
    ModLoader Loader,
    int Limit = 20);

public sealed record ModSearchResult(
    string ProjectId, // usamos o slug como identidade estável (vira ProjectSlug)
    string Title,
    string Description,
    string? IconUrl,
    int Downloads);