using System.Net.Http.Json;
using System.Text.Json.Serialization;
using TCMine_Application.Abstractions;
using TCMine_Application.Contracts;
using TCMine_Domain.Modpack;
using TCMine_Server.Infrastructure.Server;

namespace TCMine_Server.Infrastructure.CurseForge;

/// <summary>
/// Implementação de <see cref="ICurseForgeApi"/> no servidor: fala direto com a API oficial
/// do CurseForge (<c>api.curseforge.com</c>), injetando a <c>x-api-key</c> guardada (cifrada)
/// nas settings. É a única ponta que conhece a key — o launcher fala com o proxy, nunca aqui.
///
/// A key é lida por requisição (e não fixada no <see cref="HttpClient"/>) porque o Owner pode
/// configurá-la/alterá-la em runtime pelo painel, sem reiniciar o servidor.
/// </summary>
public sealed class CurseForgeApiClient(IHttpClientFactory factory, ServerSettingsService settings) : ICurseForgeApi
{
    // Nome do HttpClient registrado no DI (base = api.curseforge.com)
    public const string HttpClientName = "curseforge";

    // Constantes da API CurseForge (Minecraft e classes de conteúdo)
    private const int GameMinecraft = 432;
    private const int ClassMods = 6;
    private const int ClassModpacks = 4471;

    /// <inheritdoc />
    public async Task<CfFileRefDto?> GetLatestFileAsync(long projectId, CancellationToken ct = default)
    {
        // Busca o mod para descobrir o arquivo principal (o .zip do modpack)
        var mod = await SendAsync<CfDataEnvelope<CfModResponse>>(
            HttpMethod.Get, $"v1/mods/{projectId}", ct: ct);
        var data = mod?.Data;
        if (data is null) return null;

        // O arquivo principal costuma vir embutido em latestFiles; senão, resolve por id
        var main = data.LatestFiles.FirstOrDefault(f => f.Id == data.MainFileId);

        if (main is not null || data.MainFileId <= 0) return main is null ? null : ToDto(main);

        var files = await GetFilesAsync([data.MainFileId], ct);
        return files.TryGetValue(data.MainFileId, out var resolved) ? resolved : null;
    }

    /// <inheritdoc />
    public async Task<Stream> OpenStreamAsync(string url, CancellationToken ct = default)
    {
        // Download do CDN público (edge.forgecdn.net) — sem key, sem base address
        var http = factory.CreateClient();
        var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStreamAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<long, CfFileRefDto>> GetFilesAsync(
        IReadOnlyCollection<long> fileIds, CancellationToken ct = default)
    {
        if (fileIds.Count == 0) return new Dictionary<long, CfFileRefDto>();

        // POST em lote — evita N chamadas (um arquivo por mod do manifesto)
        var resp = await SendAsync<CfDataEnvelope<List<CfFileResponse>>>(
            HttpMethod.Post, "v1/mods/files", new { fileIds }, ct);

        return (resp?.Data ?? [])
            .Select(ToDto)
            .ToDictionary(f => f.Id);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<long, CfModRefDto>> GetModsAsync(
        IReadOnlyCollection<long> modIds, CancellationToken ct = default)
    {
        if (modIds.Count == 0) return new Dictionary<long, CfModRefDto>();

        var resp = await SendAsync<CfDataEnvelope<List<CfModResponse>>>(
            HttpMethod.Post, "v1/mods", new { modIds }, ct);

        return (resp?.Data ?? [])
            .Select(m => new CfModRefDto(m.Id, m.Name, m.ClassId))
            .ToDictionary(m => m.Id);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<long, CfLatestFileDto>> GetLatestFileIndexesAsync(
        IReadOnlyCollection<long> modIds, string gameVersion, int loaderType, CancellationToken ct = default)
    {
        if (modIds.Count == 0) return new Dictionary<long, CfLatestFileDto>();

        var result = new Dictionary<long, CfLatestFileDto>();

        // CF limita o tamanho do lote — fatiamos em blocos para muitos mods (economia: ainda 1 req/bloco)
        foreach (var chunk in modIds.Distinct().Chunk(200))
        {
            var resp = await SendAsync<CfDataEnvelope<List<CfModResponse>>>(
                HttpMethod.Post, "v1/mods", new { modIds = chunk }, ct);

            foreach (var mod in resp?.Data ?? [])
            {
                // latestFilesIndexes: arquivo mais recente por (gameVersion, loader). Filtra pelo alvo
                // e pega o de maior fileId (o mais novo). modLoader nulo = compatível com qualquer loader.
                var best = (mod.LatestFilesIndexes ?? [])
                    .Where(i => string.Equals(i.GameVersion, gameVersion, StringComparison.OrdinalIgnoreCase)
                                && (i.ModLoader is null || i.ModLoader == loaderType))
                    .OrderByDescending(i => i.FileId)
                    .FirstOrDefault();

                if (best is not null)
                    result[mod.Id] = new CfLatestFileDto(mod.Id, best.FileId, best.FileName ?? string.Empty);
            }
        }

        return result;
    }

    /// <summary>Indica se a key do CurseForge está configurada (habilita a busca na UI).</summary>
    public async Task<bool> IsConfiguredAsync(CancellationToken ct = default)
    {
        return !string.IsNullOrWhiteSpace(await settings.GetCfApiKeyAsync(ct));
    }

    /// <summary>Pesquisa mods de Minecraft por nome (opcionalmente filtrados pela versão MC).</summary>
    public Task<List<CfSearchResultDto>> SearchModsAsync(
        string query, string? gameVersion = null, CancellationToken ct = default)
    {
        return SearchAsync(ClassMods, query, gameVersion, ct);
    }

    /// <summary>Pesquisa modpacks de Minecraft por nome.</summary>
    public Task<List<CfSearchResultDto>> SearchModpacksAsync(string query, CancellationToken ct = default)
    {
        return SearchAsync(ClassModpacks, query, null, ct);
    }

    private async Task<List<CfSearchResultDto>> SearchAsync(
        int classId, string query, string? gameVersion, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        // sortField=2 (popularidade), desc — espelha a UX do backup
        var url = $"v1/mods/search?gameId={GameMinecraft}&classId={classId}" +
                  $"&searchFilter={Uri.EscapeDataString(query)}&sortField=2&sortOrder=desc&pageSize=20";
        if (!string.IsNullOrWhiteSpace(gameVersion))
            url += $"&gameVersion={Uri.EscapeDataString(gameVersion)}";

        var resp = await SendAsync<CfDataEnvelope<List<CfModResponse>>>(HttpMethod.Get, url, ct: ct);
        return (resp?.Data ?? [])
            .Select(m => new CfSearchResultDto(m.Id, m.Name, m.Summary, m.Logo?.Url, m.ClassId))
            .ToList();
    }

    /// <summary>
    /// Arquivos de um mod (mais recentes primeiro), filtrados por versão MC e tipo de loader.
    /// Usado pela UI para resolver o jar a instalar ao adicionar um mod da busca.
    /// </summary>
    public async Task<List<CfFileRefDto>> GetModFilesAsync(
        long modId, string? gameVersion = null, int? loaderType = null, CancellationToken ct = default)
    {
        var url = $"v1/mods/{modId}/files?pageSize=50";
        if (!string.IsNullOrWhiteSpace(gameVersion))
            url += $"&gameVersion={Uri.EscapeDataString(gameVersion)}";
        if (loaderType is { } lt)
            url += $"&modLoaderType={lt}";

        var resp = await SendAsync<CfDataEnvelope<List<CfFileResponse>>>(HttpMethod.Get, url, ct: ct);
        return (resp?.Data ?? []).Select(ToDto).ToList();
    }

    /// <summary>Tipo de loader do CurseForge (modLoaderType) para o filtro de arquivos.</summary>
    public static int ModLoaderType(ModLoader loader)
    {
        return loader switch
        {
            ModLoader.Forge => 1,
            ModLoader.Fabric => 4,
            ModLoader.Quilt => 5,
            _ => 6
        };
    }

    /// <summary>Mapeia o arquivo da API para o DTO neutro do Core (incl. dependências obrigatórias).</summary>
    private static CfFileRefDto ToDto(CfFileResponse f)
    {
        // relationType 3 = required dependency (1=embedded, 2=optional, 4=tool, 5=incompatible, 6=include)
        var requiredDeps = (f.Dependencies ?? [])
            .Where(d => d.RelationType == 3)
            .Select(d => d.ModId)
            .Distinct()
            .ToList();

        return new CfFileRefDto(f.Id, f.ModId, f.FileName, f.DownloadUrl, f.DisplayName, f.ServerPackFileId, requiredDeps);
    }

    /// <summary>
    /// Envia uma requisição autenticada (x-api-key) e deserialize a resposta. O corpo,
    /// quando presente, é serializado como JSON. A key é resolvida a cada chamada.
    /// </summary>
    private async Task<T?> SendAsync<T>(
        HttpMethod method, string path, object? body = null, CancellationToken ct = default)
    {
        var apiKey = await settings.GetCfApiKeyAsync(ct);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "Token do CurseForge não configurado. Defina-o em Configurações antes de importar.");

        var http = factory.CreateClient(HttpClientName);
        using var req = new HttpRequestMessage(method, path);
        req.Headers.Add("x-api-key", apiKey);
        if (body is not null) req.Content = JsonContent.Create(body);

        var resp = await http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<T>(ct);
    }

    // ── Modelos de desserialização da API (internos; o resto do aplicativo usa os DTOs do Core) ──

    private sealed record CfDataEnvelope<T>([property: JsonPropertyName("data")] T? Data);

    private sealed record CfModResponse(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("classId")]
        long ClassId,
        [property: JsonPropertyName("mainFileId")]
        long MainFileId,
        [property: JsonPropertyName("latestFiles")]
        List<CfFileResponse> LatestFiles,
        [property: JsonPropertyName("summary")]
        string? Summary = null,
        [property: JsonPropertyName("logo")] CfLogoResponse? Logo = null,
        [property: JsonPropertyName("latestFilesIndexes")]
        List<CfFileIndexResponse>? LatestFilesIndexes = null);

    // Índice compacto de arquivo recente por (gameVersion, loader) — sem url; resolvemos depois se preciso
    private sealed record CfFileIndexResponse(
        [property: JsonPropertyName("gameVersion")]
        string? GameVersion,
        [property: JsonPropertyName("fileId")] long FileId,
        [property: JsonPropertyName("filename")]
        string? FileName,
        [property: JsonPropertyName("modLoader")]
        int? ModLoader);

    private sealed record CfLogoResponse([property: JsonPropertyName("url")] string? Url);

    private sealed record CfFileResponse(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("modId")] long ModId,
        [property: JsonPropertyName("fileName")]
        string FileName,
        [property: JsonPropertyName("displayName")]
        string? DisplayName,
        [property: JsonPropertyName("downloadUrl")]
        string? DownloadUrl,
        [property: JsonPropertyName("serverPackFileId")]
        long? ServerPackFileId,
        [property: JsonPropertyName("dependencies")]
        List<CfDependencyResponse>? Dependencies = null);

    // Dependência declarada por um arquivo: outro mod + o tipo de relação (3 = obrigatória)
    private sealed record CfDependencyResponse(
        [property: JsonPropertyName("modId")] long ModId,
        [property: JsonPropertyName("relationType")]
        int RelationType);
}