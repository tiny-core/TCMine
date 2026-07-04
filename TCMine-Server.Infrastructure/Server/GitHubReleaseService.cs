using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TCMine_Server.Infrastructure.Server;

/// <summary>Faixa de releases do servidor (tags <c>server-v*</c>) — dirige o aviso de "atualize o servidor".</summary>
public sealed record ServerTrack(
    string CurrentVersion, string? LatestVersion, string? Notes, string? Url, bool UpdateAvailable);

/// <summary>
///     Faixa de releases do launcher (tags <c>launcher-v*</c>) — a versão do <b>código</b> do launcher,
///     independente do servidor. O servidor compila o launcher <b>nesta</b> versão, buscando a fonte na
///     <see cref="Tag" /> (tarball do GitHub).
/// </summary>
public sealed record LauncherTrack(
    string? LatestVersion, string? Tag, string? Notes, string? Url);

/// <summary>As duas faixas independentes.</summary>
public sealed record GitHubTracks(ServerTrack Server, LauncherTrack Launcher);

/// <summary>
///     Lê as releases do GitHub (<c>tiny-core/TCMine</c>) e separa em duas faixas por prefixo de tag:
///     <c>server-v*</c> (a imagem) e <c>launcher-v*</c> (o código do launcher). Assim atualizar um não
///     obriga o outro — resolve a sobrecarga do modelo de versão única. Cache 1h, tolerante a falha
///     (devolve o último conhecido).
/// </summary>
public sealed class GitHubReleaseService(
    IHttpClientFactory http, IConfiguration config, ILogger<GitHubReleaseService> logger)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(6);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private GitHubTracks? _cached;
    private DateTime _cachedAtUtc;

    // Repo fixo do projeto no GitHub — as faixas server-v*/launcher-v* saem daqui.
    public string Repo => "tiny-core/TCMine";

    /// <summary>As duas faixas (cacheadas). <paramref name="force" /> ignora a cache.</summary>
    public async Task<GitHubTracks> GetAsync(bool force = false, CancellationToken ct = default)
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
                $"https://api.github.com/repos/{Repo}/releases?per_page=100", ct) ?? [];

            var live = releases.Where(r => r is { Draft: false }).ToList();
            var server = Highest(live, "server-v");
            var launcher = Highest(live, "launcher-v");

            // Compara a versão SEM o prefixo "server-v": passar a tag crua ao IsNewer fazia o Parse cortar
            // em "server" (o '-') → 0.0.0 → o aviso de atualização do servidor nunca aparecia.
            var serverVersion = server?.TagName is { } st ? Strip(st, "server-v") : null;
            var serverTrack = new ServerTrack(
                current, serverVersion, server?.Body, server?.HtmlUrl,
                serverVersion is not null && AppVersion.IsNewer(serverVersion, current));

            var launcherTrack = new LauncherTrack(
                launcher?.TagName is { } lt ? Strip(lt, "launcher-v") : null,
                launcher?.TagName, launcher?.Body, launcher?.HtmlUrl);

            _cached = new GitHubTracks(serverTrack, launcherTrack);
            _cachedAtUtc = DateTime.UtcNow;
            return _cached;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao consultar releases do GitHub ({Repo}).", Repo);
            return _cached ?? new GitHubTracks(
                new ServerTrack(current, null, null, null, false),
                new LauncherTrack(null, null, null, null));
        }
        finally
        {
            _gate.Release();
        }
    }

    // Release mais alta (semver) cuja tag começa pelo prefixo
    private static GhRelease? Highest(IEnumerable<GhRelease> releases, string prefix)
    {
        return releases
            .Where(r => r.TagName?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true)
            .OrderByDescending(r => r.TagName, Comparer<string?>.Create(AppVersion.Compare))
            .FirstOrDefault();
    }

    private static string Strip(string tag, string prefix)
    {
        return tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? tag[prefix.Length..] : tag.TrimStart('v', 'V');
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
