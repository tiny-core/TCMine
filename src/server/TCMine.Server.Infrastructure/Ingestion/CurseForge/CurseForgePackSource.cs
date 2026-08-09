using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Infrastructure.Ingestion.CurseForge;

/// <summary>
///     Importa modpacks do CurseForge.
///     O pack é um .zip com manifest.json (versão do MC, loader e a lista de
///     mods como pares projeto/arquivo) e uma pasta overrides/ com configs e
///     scripts. Aqui traduzimos isso para o modelo do TCMine; quem grava é o
///     caso de uso.
/// </summary>
public sealed partial class CurseForgePackSource(
    CurseForgeApiClient api,
    HttpClient http,
    ILogger<CurseForgePackSource> logger) : IUpstreamPackSource
{
    /// <summary>Categoria "Modpacks" dentro do Minecraft no CurseForge.</summary>
    private const int ModpacksClassId = 4471;

    /// <summary>
    ///     Teto do zip do pack. Packs grandes passam de 100 MB por causa dos
    ///     overrides; acima disso é sinal de coisa errada, não de pack.
    /// </summary>
    private const long MaxPackBytes = 512L * 1024 * 1024;

    private readonly ILogger<CurseForgePackSource> _logger = logger;

    public ModFileOrigin Origin => ModFileOrigin.CurseForge;

    public ValueTask<bool> IsAvailableAsync(CancellationToken ct) => new(api.HasApiKeyAsync(ct));

    public async Task<IReadOnlyList<UpstreamPackSummary>> SearchPacksAsync(
        string text, int limit, CancellationToken ct)
    {
        var url = $"/v1/mods/search?gameId={CurseForgeApiClient.MinecraftGameId}"
                  + $"&classId={ModpacksClassId}"
                  + $"&searchFilter={Uri.EscapeDataString(text)}"
                  + $"&pageSize={limit}"
                  + "&sortField=2&sortOrder=desc"; // 2 = popularidade

        var response = await api.GetAsync(
            url, CurseForgeJsonContext.Default.CurseForgeResponseIReadOnlyListCurseForgeMod, ct);

        if (response?.Data is null)
            return [];

        return
        [
            .. response.Data.Select(m => new UpstreamPackSummary(
                m.Id.ToString(CultureInfo.InvariantCulture),
                m.Name ?? m.Slug ?? "",
                m.Summary ?? "",
                m.Logo?.ThumbnailUrl,
                null))
        ];
    }

    public async Task<UpstreamRelease?> GetLatestReleaseAsync(string projectId, CancellationToken ct)
    {
        if (!int.TryParse(projectId, out var modId))
            return null;

        var response = await api.GetAsync(
            $"/v1/mods/{modId}/files?pageSize=50",
            CurseForgeJsonContext.Default.CurseForgeResponseIReadOnlyListCurseForgeFile, ct);

        // releaseType 1 = release. Um pack costuma ter beta/alpha no meio, e
        // atualizar alguém para uma alpha sem pedir seria abuso de confiança.
        var latest = response?.Data?
            .Where(f => f.ReleaseType == 1)
            .MaxBy(f => f.FileDate);

        return latest is null
            ? null
            : new UpstreamRelease(
                latest.Id.ToString(CultureInfo.InvariantCulture),
                latest.FileName ?? latest.Id.ToString(CultureInfo.InvariantCulture),
                latest.FileDate);
    }

    public async Task<UpstreamPack?> FetchAsync(string projectId, string? fileId, CancellationToken ct)
    {
        if (!int.TryParse(projectId, out var modId))
            return null;

        var file = await FindPackFileAsync(modId, fileId, ct);
        if (file?.DownloadUrl is null)
        {
            // Sem downloadUrl o autor bloqueou distribuição por terceiros; nesse
            // caso nem o zip do pack podemos buscar.
            LogNoDownloadUrl(projectId);
            return null;
        }

        var mod = await GetModAsync(modId, ct);

        using var zipStream = await DownloadAsync(new Uri(file.DownloadUrl), ct);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var manifest = ReadManifest(archive);
        if (manifest?.Minecraft?.Version is null)
        {
            LogInvalidManifest(projectId);
            return null;
        }

        var (loader, loaderVersion) = ParseLoader(manifest.Minecraft.ModLoaders);
        var overrides = ReadOverrides(archive, manifest.Overrides ?? "overrides");

        return new UpstreamPack
        {
            ProjectId = projectId,
            FileId = file.Id.ToString(CultureInfo.InvariantCulture),
            VersionLabel = manifest.Version ?? file.FileName ?? "",
            Name = manifest.Name ?? mod?.Name ?? "Modpack importado",
            Author = manifest.Author,
            MinecraftVersion = manifest.Minecraft.Version,
            Loader = loader,
            LoaderVersion = loaderVersion,
            Mods = await WithNamesAsync(manifest.Files, ct),
            Overrides = overrides
        };
    }

    /// <summary>
    ///     Traduz os arquivos do manifest e enriquece com o nome de cada mod.
    ///     O manifest só traz ids; sem o nome, o acompanhamento da ingestão
    ///     mostraria "Baixando 927874", que não diz nada a ninguém. O endpoint em
    ///     lote resolve isso numa chamada (limite de 1000 ids por requisição).
    /// </summary>
    private async Task<IReadOnlyList<UpstreamPackMod>> WithNamesAsync(
        IReadOnlyList<CurseForgeManifestFile> files, CancellationToken ct)
    {
        var names = new Dictionary<int, string>();

        foreach (var chunk in files.Select(f => f.ProjectId).Distinct().Chunk(1000))
        {
            var response = await api.PostAsync(
                "/v1/mods",
                new CurseForgeModsRequest { ModIds = chunk },
                CurseForgeJsonContext.Default.CurseForgeModsRequest,
                CurseForgeJsonContext.Default.CurseForgeResponseIReadOnlyListCurseForgeMod,
                ct);

            foreach (var mod in response?.Data ?? [])
            {
                if (mod.Name is { Length: > 0 })
                    names[mod.Id] = mod.Name;
            }
        }

        return
        [
            .. files.Select(f => new UpstreamPackMod(
                f.ProjectId.ToString(CultureInfo.InvariantCulture),
                f.FileId.ToString(CultureInfo.InvariantCulture),
                f.Required,
                names.GetValueOrDefault(f.ProjectId)))
        ];
    }

    private async Task<CurseForgeFile?> FindPackFileAsync(int modId, string? fileId, CancellationToken ct)
    {
        var response = await api.GetAsync(
            $"/v1/mods/{modId}/files?pageSize=50",
            CurseForgeJsonContext.Default.CurseForgeResponseIReadOnlyListCurseForgeFile, ct);

        var files = response?.Data;
        if (files is null || files.Count is 0)
            return null;

        if (fileId is not null && int.TryParse(fileId, out var wanted))
            return files.FirstOrDefault(f => f.Id == wanted);

        return files.Where(f => f.ReleaseType == 1).MaxBy(f => f.FileDate) ?? files.MaxBy(f => f.FileDate);
    }

    private async Task<CurseForgeMod?> GetModAsync(int modId, CancellationToken ct)
    {
        var response = await api.GetAsync(
            $"/v1/mods/{modId}", CurseForgeJsonContext.Default.CurseForgeResponseCurseForgeMod, ct);
        return response?.Data;
    }

    /// <summary>
    ///     Baixa o zip para memória. O ZipArchive precisa de stream com busca, e
    ///     o da rede não tem — copiar é o preço.
    /// </summary>
    private async Task<MemoryStream> DownloadAsync(Uri url, CancellationToken ct)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is > MaxPackBytes)
            throw new InvalidOperationException("O pack excede o tamanho máximo aceito.");

        var buffer = new MemoryStream();
        await using (var source = await response.Content.ReadAsStreamAsync(ct))
            await source.CopyToAsync(buffer, ct);

        buffer.Position = 0;
        return buffer;
    }

    private static CurseForgeManifest? ReadManifest(ZipArchive archive)
    {
        var entry = archive.GetEntry("manifest.json");
        if (entry is null)
            return null;

        using var stream = entry.Open();
        return JsonSerializer.Deserialize(
            stream, CurseForgeJsonContext.Default.CurseForgeManifest);
    }

    private static List<UpstreamPackOverride> ReadOverrides(ZipArchive archive, string overridesFolder)
    {
        var prefix = overridesFolder.TrimEnd('/') + "/";
        var result = new List<UpstreamPackOverride>();

        foreach (var entry in archive.Entries)
        {
            // Pastas vêm como entradas de tamanho zero terminadas em "/".
            if (entry.FullName.EndsWith('/') || !entry.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var relative = entry.FullName[prefix.Length..];

            // Zip slip: uma entrada com ".." escaparia da pasta da instância ao
            // ser gravada. O caminho vem de arquivo de terceiro — não confiamos.
            if (relative.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
                continue;

            using var stream = entry.Open();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);

            result.Add(new UpstreamPackOverride(relative.Replace('\\', '/'), memory.ToArray()));
        }

        return result;
    }

    /// <summary>
    ///     O manifest traz o loader como "neoforge-21.1.100". Separamos o nome da
    ///     versão porque o nosso domínio guarda os dois em campos distintos.
    /// </summary>
    private static (ModLoader Loader, string? Version) ParseLoader(
        IReadOnlyList<CurseForgeManifestLoader> loaders)
    {
        // Um pack pode listar vários loaders; o marcado como primary é o que vale.
        var primary = loaders.FirstOrDefault(l => l.Primary)
                      ?? (loaders.Count > 0 ? loaders[0] : null);

        if (primary?.Id is null)
            return (ModLoader.Vanilla, null);

        var separator = primary.Id.IndexOf('-', StringComparison.Ordinal);
        var name = separator < 0 ? primary.Id : primary.Id[..separator];
        var version = separator < 0 ? null : primary.Id[(separator + 1)..];

        var loader = name.ToLowerInvariant() switch
        {
            "neoforge" => ModLoader.NeoForge,
            "forge" => ModLoader.Forge,
            "fabric" => ModLoader.Fabric,
            "quilt" => ModLoader.Quilt,
            _ => ModLoader.Vanilla
        };

        return (loader, version);
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Pack '{ProjectId}' do CurseForge sem URL de download (redistribuição negada).")]
    private partial void LogNoDownloadUrl(string projectId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "manifest.json ausente ou inválido no pack '{ProjectId}'.")]
    private partial void LogInvalidManifest(string projectId);
}
