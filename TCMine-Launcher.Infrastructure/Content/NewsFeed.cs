using System.Net.Http.Json;
using System.Text.Json;
using TCMine_Application.Contracts;
using TCMine_Application.Launcher;
using TCMine_Launcher.Infrastructure.Configuration;

namespace TCMine_Launcher.Infrastructure.Content;

/// <summary>Lê o feed público de novidades (<c>/api/news</c>). Implementa <see cref="INewsFeed" />.</summary>
public sealed class NewsFeed(ServerConfig config) : INewsFeed, IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // HttpClient próprio (handler custom) — descartado no Dispose.
    private readonly HttpClient _http = new(CreateHandler()) { Timeout = TimeSpan.FromSeconds(30) };

    public void Dispose()
    {
        _http.Dispose();
    }

    public async Task<IReadOnlyList<NewsItemDto>> GetNewsAsync(CancellationToken ct = default)
    {
        return await _http.GetFromJsonAsync<List<NewsItemDto>>(config.Resolve("/api/news"), Json, ct) ?? [];
    }

    private static HttpClientHandler CreateHandler()
    {
        var handler = new HttpClientHandler();
#if DEBUG
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
#endif
        return handler;
    }
}