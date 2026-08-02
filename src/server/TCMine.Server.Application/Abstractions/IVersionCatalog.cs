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
    Task<IReadOnlyList<string>> GetMinecraftVersionsAsync(bool releasesOnly, CancellationToken ct);

    Task<IReadOnlyList<string>> GetLoaderVersionsAsync(
        ModLoader loader, string minecraftVersion, bool releasesOnly, CancellationToken ct);
}
