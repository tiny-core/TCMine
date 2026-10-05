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

    /// <summary>
    ///     As releases de um mod que servem à versão do Minecraft e ao loader do
    ///     pack, da mais nova para a mais velha. É o que deixa o admin fixar uma
    ///     versão em vez de levar sempre "a mais recente": o id escolhido vira o
    ///     FileId da ingestão. Lista vazia = nenhuma compatível (ou a origem não
    ///     respondeu) — a busca é interativa e não lança.
    /// </summary>
    Task<IReadOnlyList<UpstreamRelease>> ListVersionsAsync(
        string projectId, string minecraftVersion, ModLoader loader, CancellationToken ct);
}

public sealed record ModSearchQuery(
    string Text,
    string MinecraftVersion,
    ModLoader Loader,
    int Limit = 20);

/// <summary>
///     Um resultado de busca. <paramref name="Compatible" /> diz se o mod tem
///     arquivo para a versão do Minecraft e o loader do modpack.
///     Existe porque filtrar a BUSCA por compatibilidade era pior que inútil: ao
///     procurar "Mekanism" numa versão recém-lançada do Minecraft o admin recebia
///     "nenhum mod encontrado", o que sugere que o mod não existe — quando na
///     verdade ele existe e só ainda não saiu para aquela versão. Mostrar e
///     marcar responde a pergunta; esconder inventa outra.
/// </summary>
public sealed record ModSearchResult(
    string ProjectId, // id do projeto na origem: identidade estável (vira ProjectSlug)
    string Title,
    string Description,
    string? IconUrl,
    int Downloads,
    bool Compatible = true,
    string? LatestVersions = null);
