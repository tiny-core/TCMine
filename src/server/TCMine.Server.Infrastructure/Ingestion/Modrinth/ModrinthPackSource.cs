using System.IO.Compression;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Infrastructure.Ingestion.Modrinth;

/// <summary>
///     Importa modpacks do Modrinth (.mrpack).
///     Diferenças relevantes em relação ao CurseForge: não precisa de chave de
///     API, o índice já traz o ambiente de cada mod, e nenhum autor pode negar
///     redistribuição — logo, não existe a figura do "mod pendente" aqui.
///     A pegadinha é que o índice identifica os mods pela URL do CDN, não por
///     projeto/versão; extraímos os dois da URL para manter o mesmo modelo de
///     procedência do resto do sistema.
/// </summary>
public sealed partial class ModrinthPackSource(
    HttpClient http,
    ILogger<ModrinthPackSource> logger) : IUpstreamPackSource
{
    /// <summary>Mesmo teto do CurseForge: acima disso é sinal de coisa errada.</summary>
    private const long MaxPackBytes = 512L * 1024 * 1024;

    private static readonly string[] ModpackFacet = ["project_type:modpack"];
    private static readonly string[] OverridePrefixes = ["overrides/", "client-overrides/", "server-overrides/"];

    private readonly ILogger<ModrinthPackSource> _logger = logger;

    public ModFileOrigin Origin => ModFileOrigin.Modrinth;

    // Sem chave de API: está sempre à disposição.
    public ValueTask<bool> IsAvailableAsync(CancellationToken ct) => ValueTask.FromResult(true);

    public async Task<IReadOnlyList<UpstreamPackSummary>> SearchPacksAsync(
        string text, int limit, CancellationToken ct)
    {
        var facets = JsonSerializer.Serialize(new[] { ModpackFacet });
        var url = $"/v2/search?query={Uri.EscapeDataString(text)}"
                  + $"&facets={Uri.EscapeDataString(facets)}"
                  + $"&limit={limit}";

        try
        {
            var response = await http.GetFromJsonAsync(
                url, ModrinthJsonContext.Default.ModrinthPackSearchResponse, ct);

            return
            [
                .. (response?.Hits ?? []).Select(h => new UpstreamPackSummary(
                    h.Slug ?? h.ProjectId ?? "",
                    h.Title ?? "",
                    h.Description ?? "",
                    h.IconUrl,
                    h.Author))
            ];
        }
        catch (HttpRequestException ex)
        {
            LogSearchError(ex, text);
            return [];
        }
    }

    public async Task<UpstreamRelease?> GetLatestReleaseAsync(string projectId, CancellationToken ct)
    {
        var version = await LatestVersionAsync(projectId, ct);

        return version is null
            ? null
            : new UpstreamRelease(version.Id, version.VersionNumber ?? version.Id, version.DatePublished);
    }

    public async Task<UpstreamPack?> FetchAsync(string projectId, string? fileId, CancellationToken ct)
    {
        try
        {
            var version = fileId is { Length: > 0 }
                ? await http.GetFromJsonAsync(
                    $"/v2/version/{Uri.EscapeDataString(fileId)}",
                    ModrinthJsonContext.Default.ModrinthPackVersion, ct)
                : await LatestVersionAsync(projectId, ct);

            // O .mrpack é o arquivo primário da versão; o resto (se houver) são
            // extras que não descrevem o pack.
            var file = version?.Files.FirstOrDefault(f => f.Primary)
                       ?? (version?.Files.Count > 0 ? version.Files[0] : null);

            if (version is null || file?.Url is null)
                return null;

            using var packStream = await DownloadAsync(new Uri(file.Url), ct);
            using var archive = new ZipArchive(packStream, ZipArchiveMode.Read);

            var index = ReadIndex(archive);
            if (index is null)
            {
                LogInvalidIndex(projectId);
                return null;
            }

            if (!index.Dependencies.TryGetValue("minecraft", out var minecraft))
            {
                LogInvalidIndex(projectId);
                return null;
            }

            var (loader, loaderVersion) = ParseLoader(index.Dependencies);

            return new UpstreamPack
            {
                ProjectId = projectId,
                FileId = version.Id,
                VersionLabel = index.VersionId ?? version.VersionNumber ?? version.Id,
                Name = index.Name ?? version.Name ?? "Modpack importado",
                Author = null,
                IconUrl = await TryGetIconAsync(projectId, ct),
                MinecraftVersion = minecraft,
                Loader = loader,
                LoaderVersion = loaderVersion,
                Mods = [.. index.Files.Select(ToMod).OfType<UpstreamPackMod>()],
                Overrides = ReadOverrides(archive)
            };
        }
        catch (HttpRequestException ex)
        {
            LogFetchError(ex, projectId);
            return null;
        }
        catch (InvalidDataException ex)
        {
            // Zip corrompido ou resposta que não é um .mrpack.
            LogFetchError(ex, projectId);
            return null;
        }
    }

    /// <summary>
    ///     Capa do projeto. Chamada à parte porque o índice do .mrpack não a traz;
    ///     falha aqui não impede a importação — o pack entra sem capa.
    /// </summary>
    private async Task<string?> TryGetIconAsync(string projectId, CancellationToken ct)
    {
        try
        {
            var project = await http.GetFromJsonAsync(
                $"/v2/project/{Uri.EscapeDataString(projectId)}",
                ModrinthJsonContext.Default.ModrinthProject, ct);

            return project?.IconUrl;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private async Task<ModrinthPackVersion?> LatestVersionAsync(string projectId, CancellationToken ct)
    {
        try
        {
            var versions = await http.GetFromJsonAsync(
                $"/v2/project/{Uri.EscapeDataString(projectId)}/version",
                ModrinthJsonContext.Default.IReadOnlyListModrinthPackVersion, ct);

            // A API já devolve da mais nova para a mais velha, mas preferimos a
            // release estável: alpha/beta de modpack costuma quebrar mundo.
            return versions?.FirstOrDefault(v => v.VersionType == "release")
                   ?? (versions?.Count > 0 ? versions[0] : null);
        }
        catch (HttpRequestException ex)
        {
            LogFetchError(ex, projectId);
            return null;
        }
    }

    /// <summary>
    ///     Traduz um arquivo do índice para o nosso modelo.
    ///     O índice não traz projeto/versão: eles vêm embutidos na URL do CDN
    ///     (<c>/data/{projectId}/versions/{versionId}/arquivo.jar</c>). Sem isso
    ///     não haveria como detectar atualização depois nem como o UpsertFile
    ///     saber que dois .jar são o mesmo mod.
    /// </summary>
    private static UpstreamPackMod? ToMod(ModrinthPackFile file)
    {
        if (file.Downloads.Count is 0)
            return null;

        var url = file.Downloads[0];

        var match = CdnPath().Match(url);
        if (!match.Success)
            return null;

        // "required" dos dois lados = obrigatório; o resto o jogador escolhe.
        var required = file.Env is null
                       || (file.Env.Client is not "optional" && file.Env.Server is not "optional");

        return new UpstreamPackMod(
            match.Groups["project"].Value,
            match.Groups["version"].Value,
            required);
    }

    private static ModrinthPackIndex? ReadIndex(ZipArchive archive)
    {
        var entry = archive.GetEntry("modrinth.index.json");
        if (entry is null)
            return null;

        using var stream = entry.Open();
        return JsonSerializer.Deserialize(stream, ModrinthJsonContext.Default.ModrinthPackIndex);
    }

    /// <summary>
    ///     Overrides do .mrpack. Há duas pastas: <c>overrides/</c> (ambos os
    ///     lados) e <c>client-overrides/</c> / <c>server-overrides/</c>. Trazemos
    ///     as três achatadas — o lado por arquivo de override é raro e o launcher
    ///     hoje reconcilia a instância inteira.
    /// </summary>
    private static List<UpstreamPackOverride> ReadOverrides(ZipArchive archive)
    {
        var result = new List<UpstreamPackOverride>();

        foreach (var entry in archive.Entries)
        {
            var relative = StripOverridePrefix(entry.FullName);
            if (relative is null || entry.Length is 0)
                continue;

            // Zip-slip: um entry com ".." escaparia da pasta da instância.
            if (relative.Contains("..", StringComparison.Ordinal))
                continue;

            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);

            result.Add(new UpstreamPackOverride(relative, buffer.ToArray()));
        }

        return result;
    }

    private static string? StripOverridePrefix(string fullName)
    {
        foreach (var prefix in OverridePrefixes)
        {
            if (fullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return fullName[prefix.Length..].Replace('\\', '/');
        }

        return null;
    }

    /// <summary>
    ///     O loader vem como chave em "dependencies", ao lado de "minecraft".
    ///     Sem loader é pack de vanilla puro, que o modelo aceita.
    /// </summary>
    private static (ModLoader Loader, string? Version) ParseLoader(IReadOnlyDictionary<string, string> dependencies)
    {
        foreach (var (key, value) in dependencies)
        {
            var loader = key switch
            {
                "neoforge" => ModLoader.NeoForge,
                "forge" => ModLoader.Forge,
                "fabric-loader" => ModLoader.Fabric,
                "quilt-loader" => ModLoader.Quilt,
                _ => (ModLoader?)null
            };

            if (loader is { } found)
                return (found, value);
        }

        return (ModLoader.Vanilla, null);
    }

    private async Task<Stream> DownloadAsync(Uri url, CancellationToken ct)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength > MaxPackBytes)
            throw new InvalidDataException($"Pack acima do limite de {MaxPackBytes} bytes.");

        // ZipArchive precisa de stream com seek; o pack cabe em memória.
        var buffer = new MemoryStream();
        await response.Content.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        return buffer;
    }

    [GeneratedRegex(@"/data/(?<project>[^/]+)/versions/(?<version>[^/]+)/")]
    private static partial Regex CdnPath();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao buscar packs no Modrinth para '{Text}'.")]
    private partial void LogSearchError(Exception ex, string text);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao ler o pack '{ProjectId}' no Modrinth.")]
    private partial void LogFetchError(Exception ex, string projectId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pack '{ProjectId}' sem modrinth.index.json utilizável.")]
    private partial void LogInvalidIndex(string projectId);

    /// <summary>
    ///     O Modrinth não tem server pack: o .mrpack já declara o lado de cada
    ///     arquivo, então o pack importado dele nasce completo para os dois
    ///     lados e não há nada a conciliar.
    /// </summary>
    public Task<IReadOnlyDictionary<string, string>> GetFileNamesAsync(
        IReadOnlyList<string> fileIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<string, string>>(
            new Dictionary<string, string>(StringComparer.Ordinal));

    public Task<IServerPackReader?> OpenServerPackAsync(
        string projectId, string serverPackFileId, CancellationToken ct) =>
        Task.FromResult<IServerPackReader?>(null);
}
