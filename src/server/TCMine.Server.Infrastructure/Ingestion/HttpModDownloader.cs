using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Infrastructure.Ingestion;

/// <summary>
///     Download por HTTP. O HttpClient tipado traz User-Agent e resiliência do registro.
/// </summary>
public sealed class HttpModDownloader(HttpClient http) : IModDownloader
{
    public async Task<Stream> OpenAsync(Uri url, CancellationToken ct)
    {
        try
        {
            return await http.GetStreamAsync(url, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new ModDownloadException(url, ex);
        }
    }
}
