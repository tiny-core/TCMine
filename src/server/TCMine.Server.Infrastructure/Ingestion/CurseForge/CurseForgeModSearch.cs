using System.Globalization;
using TCMine.Contracts.Modpacks;
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
        // Busca SEM filtrar por versão/loader, ordenada por popularidade. O
        // filtro fica na exibição: procurar "Mekanism" numa versão recém-saída
        // do Minecraft devolvia lista vazia, e "nenhum mod encontrado" faz o
        // admin achar que digitou errado.
        var url = $"/v1/mods/search?gameId={CurseForgeApiClient.MinecraftGameId}"
                  + $"&classId={ModsClassId}"
                  + $"&searchFilter={Uri.EscapeDataString(query.Text)}"
                  + $"&sortField=2&sortOrder=desc"
                  + $"&pageSize={query.Limit}";

        var loaderType = CurseForgeApiClient.ToLoaderType(query.Loader);

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
                    (int)Math.Min(m.DownloadCount, int.MaxValue),
                    Serve(m, query.MinecraftVersion, loaderType),
                    VersoesRecentes(m)))
            ];
        }
        catch (HttpRequestException)
        {
            // Busca é interativa: devolver vazio deixa o admin tentar de novo,
            // enquanto uma exceção derrubaria o diálogo.
            return [];
        }
    }

    public async Task<IReadOnlyList<UpstreamRelease>> ListVersionsAsync(
        string projectId, string minecraftVersion, ModLoader loader, CancellationToken ct)
    {
        if (!int.TryParse(projectId, out var modId))
            return [];

        // Mesmo filtro que a ingestão usa ao resolver.
        var url = $"/v1/mods/{modId}/files?pageSize=50"
                  + $"&gameVersion={Uri.EscapeDataString(minecraftVersion)}";
        var loaderType = CurseForgeApiClient.ToLoaderType(loader);
        if (loaderType is not 0)
            url += $"&modLoaderType={loaderType}";

        try
        {
            var response = await api.GetAsync(
                url, CurseForgeJsonContext.Default.CurseForgeResponseIReadOnlyListCurseForgeFile, ct);

            return response?.Data is not { } files
                ? []
                : files
                    .OrderByDescending(f => f.FileDate)
                    .Select(f => new UpstreamRelease(
                        f.Id.ToString(CultureInfo.InvariantCulture),
                        f.FileName ?? f.Id.ToString(CultureInfo.InvariantCulture),
                        f.FileDate,
                        f.ReleaseType == 1,
                        CurseForgeApiClient.MinecraftVersionsOf(f.GameVersions)))
                    .ToArray();
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    /// <summary>Tem release para a versão e o loader do pack?</summary>
    private static bool Serve(CurseForgeMod mod, string minecraftVersion, int loaderType)
    {
        if (mod.LatestFilesIndexes.Count is 0)
            return true; // sem informação não se acusa incompatibilidade

        return mod.LatestFilesIndexes.Any(f =>
            string.Equals(f.GameVersion, minecraftVersion, StringComparison.OrdinalIgnoreCase)
            && (loaderType is 0 || f.ModLoader is null || f.ModLoader == loaderType));
    }

    /// <summary>As versões mais recentes que o mod atende, para explicar a recusa.</summary>
    private static string? VersoesRecentes(CurseForgeMod mod)
    {
        var versions = mod.LatestFilesIndexes
            .Select(f => f.GameVersion)
            .Where(v => v is { Length: > 0 })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();

        return versions.Count is 0 ? null : string.Join(", ", versions);
    }
}
