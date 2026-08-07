using System.Globalization;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Infrastructure.Ingestion.CurseForge;

/// <summary>
///     Busca mods no catálogo do CurseForge (classId 6 = Mods, dentro do jogo
///     Minecraft). Devolve o id numérico como identidade estável — no CurseForge
///     o slug pode mudar, o id não.
/// </summary>
public sealed class CurseForgeModSearch(CurseForgeApiClient api) : IModSearch
{
    /// <summary>Categoria "Mods" dentro do Minecraft no CurseForge.</summary>
    private const int ModsClassId = 6;

    public ModFileOrigin Origin => ModFileOrigin.CurseForge;

    public ValueTask<bool> IsAvailableAsync(CancellationToken ct) => new(api.HasApiKeyAsync(ct));

    public async Task<IReadOnlyList<ModSearchResult>> SearchAsync(ModSearchQuery query, CancellationToken ct)
    {
        var url = $"/v1/mods/search?gameId={CurseForgeApiClient.MinecraftGameId}"
                  + $"&classId={ModsClassId}"
                  + $"&searchFilter={Uri.EscapeDataString(query.Text)}"
                  + $"&gameVersion={Uri.EscapeDataString(query.MinecraftVersion)}"
                  + $"&pageSize={query.Limit}";

        var loaderType = CurseForgeApiClient.ToLoaderType(query.Loader);
        if (loaderType is not 0)
            url += $"&modLoaderType={loaderType}";

        try
        {
            var response = await api.GetAsync(
                url, CurseForgeJsonContext.Default.CurseForgeResponseIReadOnlyListCurseForgeMod, ct);

            if (response?.Data is null)
                return [];

            return
            [
                .. response.Data.Select(m => new ModSearchResult(
                    m.Id.ToString(CultureInfo.InvariantCulture), // id numérico = identidade estável no CurseForge
                    m.Name ?? m.Slug ?? m.Id.ToString(CultureInfo.InvariantCulture),
                    m.Summary ?? "",
                    m.Logo?.ThumbnailUrl,
                    (int)Math.Min(m.DownloadCount, int.MaxValue)))
            ];
        }
        catch (HttpRequestException)
        {
            // Busca é interativa: devolver vazio deixa o admin tentar de novo,
            // enquanto uma exceção derrubaria o diálogo.
            return [];
        }
    }
}
