using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TCMine_Server.Infrastructure.Server;

/// <summary>Resumo do estado de atualização do servidor (para o banner do painel).</summary>
public sealed record ServerUpdate(
    string CurrentVersion,
    string? LatestVersion,
    string? Notes,
    string? Url,
    bool UpdateAvailable);

/// <summary>
///     Verifica se há uma versão mais recente do TCMine publicada no GitHub (releases <c>v*</c> de
///     <c>tiny-core/TCMine</c>) e compara com a versão corrente (<see cref="AppVersion" />). Como o modelo
///     é de <b>uma versão só</b>, a mesma release cobre servidor e launcher: um update do servidor implica
///     puxar a imagem nova (que traz a fonte do launcher naquela versão).
///     Cache de 1h, tolerante a falha (devolve o último conhecido). Repo configurável via <c>GITHUB_REPO</c>.
/// </summary>
public sealed class GitHubReleaseService(
    IHttpClientFactory http, IConfiguration config, ILogger<GitHubReleaseService> logger)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(1);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private ServerUpdate? _cached;
    private DateTime _cachedAtUtc;

    private string Repo => config["GITHUB_REPO"] is { Length: > 0 } r ? r.Trim() : "tiny-core/TCMine";

    /// <summary>Estado de atualização (cacheado). <paramref name="force" /> ignora a cache.</summary>
    public async Task<ServerUpdate> GetAsync(bool force = false, CancellationToken ct = default)
    {
        var current = AppVersion.Current(config);

        if (!force && Fresh() && _cached is not null)
            return _cached;

        await _gate.WaitAsync(ct);
        try
        {
            if (!force && Fresh() && _cached is not null)
                return _cached;

            var client = http.CreateClient("github");
            var releases = await client.GetFromJsonAsync<List<GhRelease>>(
                $"https://api.github.com/repos/{Repo}/releases?per_page=100", ct);

            var latest = releases?
                .Where(r => r is { Draft: false } && r.TagName?.StartsWith('v') == true)
                .OrderByDescending(r => r.TagName, Comparer<string?>.Create(AppVersion.Compare))
                .FirstOrDefault();

            _cached = new ServerUpdate(
                current,
                latest?.TagName is { } t ? t.TrimStart('v', 'V') : null,
                latest?.Body, latest?.HtmlUrl,
                latest is not null && AppVersion.IsNewer(latest.TagName, current));
            _cachedAtUtc = DateTime.UtcNow;
            return _cached;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao consultar releases do GitHub ({Repo}).", Repo);
            // Sem rede/erro → não sinaliza update; devolve o último conhecido ou um estado neutro
            return _cached ?? new ServerUpdate(current, null, null, null, false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool Fresh()
    {
        return _cached is not null && DateTime.UtcNow - _cachedAtUtc < Ttl;
    }

    private sealed record GhRelease(
        [property: JsonPropertyName("tag_name")]
        string? TagName,
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("html_url")]
        string? HtmlUrl,
        [property: JsonPropertyName("draft")] bool Draft);
}
