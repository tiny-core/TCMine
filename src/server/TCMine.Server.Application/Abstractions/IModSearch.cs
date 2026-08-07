using TCMine.Contracts.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Busca mods numa origem. Há uma implementação por origem (Modrinth,
///     CurseForge); quem consome escolhe pela <see cref="Origin" />.
/// </summary>
public interface IModSearch
{
    ModFileOrigin Origin { get; }

    /// <summary>
    ///     Utilizável agora? O CurseForge exige API key configurada; sem ela, a
    ///     origem simplesmente não aparece para o admin.
    /// </summary>
    ValueTask<bool> IsAvailableAsync(CancellationToken ct);

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
