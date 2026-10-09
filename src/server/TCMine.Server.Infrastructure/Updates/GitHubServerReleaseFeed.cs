using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Updates;

namespace TCMine.Server.Infrastructure.Updates;

/// <summary>
///     Lê as releases do repositório no GitHub e devolve a estável mais nova do
///     SERVIDOR. O repositório publica dois produtos, então o filtro é pela tag
///     ("server-v*"), e não pelo "latest release" do GitHub — que pode muito bem
///     ser uma do launcher.
///     Cache de seis horas, com acerto e com erro: a API anônima do GitHub dá 60
///     consultas por hora por IP, e um painel aberto em várias abas não pode
///     gastá-las nem ficar martelando um GitHub fora do ar.
/// </summary>
public sealed partial class GitHubServerReleaseFeed(
    HttpClient http,
    IMemoryCache cache,
    IOptions<UpdateOptions> options,
    ILogger<GitHubServerReleaseFeed> logger) : IServerReleaseFeed
{
    private const string TagPrefix = "server-v";
    private const string CacheKey = "tcmine:server-latest-release";
    private static readonly TimeSpan CacheFor = TimeSpan.FromHours(6);

    private readonly ILogger<GitHubServerReleaseFeed> _logger = logger;

    public async Task<ServerRelease?> GetLatestStableAsync(CancellationToken ct)
    {
        if (!options.Value.Enabled)
            return null;

        if (cache.TryGetValue(CacheKey, out ServerRelease? cached))
            return cached;

        ServerRelease? latest = null;
        try
        {
            var releases = await http.GetFromJsonAsync<IReadOnlyList<GitHubRelease>>(
                $"/repos/{options.Value.Repository}/releases?per_page=50", ct);

            latest = Pick(releases ?? []);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                       or JsonException
                                   && !ct.IsCancellationRequested)
        {
            LogFalha(ex, options.Value.Repository);
        }

        cache.Set(CacheKey, latest, CacheFor);
        return latest;
    }

    /// <summary>
    ///     A estável mais nova do servidor. Rascunho e pré-lançamento ficam de
    ///     fora: avisar "há atualização" para uma beta empurraria o admin para o
    ///     que não foi feito para ele.
    /// </summary>
    internal static ServerRelease? Pick(IEnumerable<GitHubRelease> releases)
    {
        ServerRelease? best = null;
        SemanticVersion bestVersion = default;

        foreach (var r in releases)
        {
            if (r.Draft || r.Prerelease || r.TagName is null
                || !r.TagName.StartsWith(TagPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var number = r.TagName[TagPrefix.Length..];
            if (!SemanticVersion.TryParse(number, out var version) || version.PreRelease is not null)
                continue;

            if (best is not null && version <= bestVersion)
                continue;

            if (!Uri.TryCreate(r.HtmlUrl, UriKind.Absolute, out var page))
                continue;

            best = new ServerRelease(number, page, r.PublishedAt ?? DateTimeOffset.MinValue);
            bestVersion = version;
        }

        return best;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Não foi possível consultar as releases de {Repository}; o aviso de atualização fica para depois.")]
    private partial void LogFalha(Exception ex, string repository);

    internal sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")]
        string? TagName,
        [property: JsonPropertyName("html_url")]
        string? HtmlUrl,
        [property: JsonPropertyName("draft")] bool Draft,
        [property: JsonPropertyName("prerelease")]
        bool Prerelease,
        [property: JsonPropertyName("published_at")]
        DateTimeOffset? PublishedAt);
}
