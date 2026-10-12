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
///     Cache de uma hora para a resposta e de 15 minutos para a falha: a API
///     anônima do GitHub dá 60 consultas por hora por IP, e um painel aberto em
///     várias abas não pode gastá-las nem martelar um GitHub fora do ar. Eram
///     seis horas para os dois casos, e um painel aberto antes de uma release
///     levava até seis horas para avisar dela — tempo de sobra para quem
///     publicou concluir que o aviso não funcionava. O "Verificar agora" das
///     Configurações passa por cima do cache.
/// </summary>
public sealed partial class GitHubServerReleaseFeed(
    HttpClient http,
    IMemoryCache cache,
    IOptions<UpdateOptions> options,
    TimeProvider clock,
    ILogger<GitHubServerReleaseFeed> logger) : IServerReleaseFeed
{
    private const string TagPrefix = "server-v";
    private const string CacheKey = "tcmine:server-latest-release";
    private static readonly TimeSpan CacheFor = TimeSpan.FromHours(1);
    private static readonly TimeSpan CacheFailureFor = TimeSpan.FromMinutes(15);

    private readonly ILogger<GitHubServerReleaseFeed> _logger = logger;

    public async Task<ServerReleaseLookup> LookupAsync(bool refresh, CancellationToken ct)
    {
        if (!options.Value.Enabled)
            return new ServerReleaseLookup(ReleaseFeedState.Disabled, null, clock.GetUtcNow());

        if (!refresh && cache.TryGetValue(CacheKey, out ServerReleaseLookup? cached) && cached is not null)
            return cached;

        ServerReleaseLookup lookup;
        try
        {
            var releases = await http.GetFromJsonAsync<IReadOnlyList<GitHubRelease>>(
                $"/repos/{options.Value.Repository}/releases?per_page=50", ct);

            lookup = new ServerReleaseLookup(ReleaseFeedState.Ok, Pick(releases ?? []), clock.GetUtcNow());
            cache.Set(CacheKey, lookup, CacheFor);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                       or JsonException
                                   && !ct.IsCancellationRequested)
        {
            LogFalha(ex, options.Value.Repository);

            // A falha também fica em cache, mas por pouco: o bastante para não
            // martelar um GitHub fora do ar, curto o bastante para o aviso não
            // sumir por horas por causa de uma oscilação.
            lookup = new ServerReleaseLookup(ReleaseFeedState.Unreachable, null, clock.GetUtcNow());
            cache.Set(CacheKey, lookup, CacheFailureFor);
        }

        return lookup;
    }

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
