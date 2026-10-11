using TCMine.Contracts.Modpacks;

namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Lista versões de Minecraft e de loaders a partir dos canais oficiais, para
///     os dropdowns do formulário. Substitui o texto livre que deixava passar
///     versões inexistentes (e crashava o servidor). releasesOnly é por chamada —
///     cada campo do formulário tem o seu toggle.
/// </summary>
public interface IVersionCatalog
{
    Task<VersionLookup> GetMinecraftVersionsAsync(bool releasesOnly, CancellationToken ct);

    Task<VersionLookup> GetLoaderVersionsAsync(
        ModLoader loader, string minecraftVersion, bool releasesOnly, CancellationToken ct);
}

/// <summary>
///     O resultado de uma consulta ao catálogo, com a diferença que uma lista
///     sozinha não diz: "não existe nenhuma" e "não consegui saber" são respostas
///     opostas. A primeira autoriza recusar uma versão que não está na lista; a
///     segunda não autoriza nada. Antes as duas eram a mesma lista vazia, e uma
///     queda da fonte parecia "este loader não tem versão para este Minecraft".
/// </summary>
/// <param name="Versions">As versões, da mais nova para a mais antiga. Vazia em caso de falha.</param>
/// <param name="Failed">A fonte não respondeu: a lista não é uma resposta.</param>
public sealed record VersionLookup(IReadOnlyList<string> Versions, bool Failed)
{
    public static VersionLookup Unavailable { get; } = new(Array.Empty<string>(), true);

    public static VersionLookup Of(IReadOnlyList<string> versions) => new(versions, false);
}
