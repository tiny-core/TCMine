namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Baixa os bytes de um mod já resolvido. A Application define a porta; a
///     implementação (HttpClient) vive na Infrastructure. Sem isto, a ingestão
///     dependeria de HttpClient direto — infra na camada de casos de uso — e seria
///     difícil de testar sem rede.
/// </summary>
public interface IModDownloader
{
    Task<Stream> OpenAsync(Uri url, CancellationToken ct);
}

/// <summary>
///     Falha ao baixar um mod. A porta traduz o erro de transporte (HttpRequestException)
///     nesta exceção própria, para o System.Net.Http não escapar para a Application.
/// </summary>
public sealed class ModDownloadException(Uri url, Exception inner)
    : Exception($"Falha ao baixar {url}.", inner);
