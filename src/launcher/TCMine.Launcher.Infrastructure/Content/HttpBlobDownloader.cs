using Microsoft.Extensions.Logging;
using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Infrastructure.Content;

/// <summary>
///     Baixa blobs do content store do servidor, por hash.
///     Usa o mesmo pote de cookies do resto: o endpoint exige sessão, e passar
///     credencial de novo aqui criaria um segundo caminho para manter em dia.
/// </summary>
public sealed partial class HttpBlobDownloader(
    HttpClient http,
    ILogger<HttpBlobDownloader> logger) : IBlobDownloader
{
    private readonly ILogger<HttpBlobDownloader> _logger = logger;

    public async Task<Stream> OpenAsync(Uri serverUrl, string sha256, CancellationToken ct)
    {
        var endpoint = new Uri(serverUrl, $"/api/v1/blobs/{sha256}");

        // ResponseHeadersRead: sem isto o HttpClient bufferiza o corpo inteiro em
        // memória antes de devolver, e um modpack de centenas de megabytes viraria
        // centenas de megabytes de heap.
        var resposta = await http.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!resposta.IsSuccessStatusCode)
        {
            LogFalhou(endpoint, (int)resposta.StatusCode);
            resposta.Dispose();

            throw new HttpRequestException(
                $"O servidor respondeu {(int)resposta.StatusCode} ao baixar {sha256[..8]}.");
        }

        return await resposta.Content.ReadAsStreamAsync(ct);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Download de {Endpoint} respondeu {StatusCode}.")]
    private partial void LogFalhou(Uri endpoint, int statusCode);
}
