using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using TCMine_Application.Contracts;
using TCMine_Application.Launcher;

namespace TCMine_Launcher.Infrastructure;

/// <summary>Lê o feed público de novidades (<c>/api/news</c>). Implementa <see cref="INewsFeed"/>.</summary>
public sealed class NewsFeed(ServerConfig config) : INewsFeed
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http = new(CreateHandler()) { Timeout = TimeSpan.FromSeconds(30) };

    public async Task<IReadOnlyList<NewsItemDto>> GetNewsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<NewsItemDto>>(config.Resolve("/api/news"), Json, ct) ?? [];

    private static HttpMessageHandler CreateHandler()
    {
        var handler = new HttpClientHandler();
#if DEBUG
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
#endif
        return handler;
    }
}
