using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Infrastructure.Network;

/// <summary>
///     Descobre o IP público perguntando a quem recebe a conexão.
///     Duas fontes, em ordem: o trace da Cloudflare e, se ele falhar, o ipify. As
///     duas são gratuitas e sem chave; nenhuma tem contrato connosco, e por isso
///     são duas — a segunda existe para a primeira poder sair do ar.
///     Cache de dez minutos no acerto: o IP de casa muda poucas vezes por mês, e
///     esta resposta entra em telas e na lista de servidores do launcher. No erro
///     o cache é de um minuto — o bastante para uma rede fora do ar não custar o
///     tempo limite a cada tela aberta, sem prender a detecção depois que a rede
///     volta.
///     Sem a resiliência padrão dos outros clientes, e de propósito: ela tenta de
///     novo por até trinta segundos, e isto roda no caminho da lista de servidores
///     do launcher. A segunda fonte já é a nova tentativa.
/// </summary>
public sealed partial class PublicAddressProvider(
    HttpClient http,
    IMemoryCache cache,
    ILogger<PublicAddressProvider> logger) : IPublicAddressProvider
{
    private const string CacheKey = "tcmine:public-address";

    private static readonly Uri CloudflareTrace = new("https://www.cloudflare.com/cdn-cgi/trace");
    private static readonly Uri Ipify = new("https://api.ipify.org");

    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(1);

    private readonly ILogger<PublicAddressProvider> _logger = logger;

    public async Task<string?> GetAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey, out string? cached))
            return cached;

        var address = await TryAsync(CloudflareTrace, ParseTrace, ct)
                      ?? await TryAsync(Ipify, ParsePlain, ct);

        if (address is null)
            LogUndetected();

        cache.Set(CacheKey, address, address is null ? RetryAfter : CacheFor);
        return address;
    }

    /// <summary>
    ///     O IP dentro do trace da Cloudflare: texto de uma chave por linha, e a
    ///     que interessa é <c>ip=</c>.
    /// </summary>
    public static string? ParseTrace(string body)
    {
        foreach (var line in body.Split('\n'))
        {
            if (line.StartsWith("ip=", StringComparison.Ordinal))
                return ParsePlain(line[3..]);
        }

        return null;
    }

    /// <summary>
    ///     O corpo inteiro é o IP (ipify). Passa pelo <see cref="IPAddress" /> para
    ///     uma página de erro, ou o portal de um Wi-Fi, nunca virar endereço de
    ///     servidor.
    /// </summary>
    public static string? ParsePlain(string body) =>
        IPAddress.TryParse(body.Trim(), out var ip) ? ip.ToString() : null;

    private async Task<string?> TryAsync(Uri source, Func<string, string?> parse, CancellationToken ct)
    {
        try
        {
            return parse(await http.GetStringAsync(source, ct));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                   && !ct.IsCancellationRequested)
        {
            LogSourceFailed(ex, source.Host);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Não foi possível consultar o IP público em {Source}.")]
    private partial void LogSourceFailed(Exception ex, string source);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "IP público não detectado: nenhuma das fontes respondeu. Nova tentativa em um minuto.")]
    private partial void LogUndetected();
}
