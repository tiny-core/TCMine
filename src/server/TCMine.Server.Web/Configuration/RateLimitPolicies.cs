using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using TCMine.Server.Application.Cloud;

namespace TCMine.Server.Web.Configuration;

/// <summary>
///     Limites de taxa por endereço de origem.
///     Aplicado por endpoint, nunca global: o circuito do Blazor e o hub SignalR
///     fazem muitas requisições legítimas em rajada, e limitá-los derrubaria o
///     painel de quem está usando — que é justamente o oposto do objetivo.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>Login (painel e launcher) e setup inicial.</summary>
    public const string AuthPolicy = "auth";

    /// <summary>Download de blob pelo launcher.</summary>
    public const string BlobPolicy = "blobs";

    /// <summary>API da nuvem chamada pelos servidores de jogo (mod tccloud).</summary>
    public const string CloudPolicy = "cloud";

    /// <summary>
    ///     Requisições por minuto por CHAVE de servidor (não por IP: vários
    ///     servidores de jogo do mesmo host Docker saem pelo mesmo endereço).
    ///     O mod manda um lote por vez e espera a resposta, então mesmo um
    ///     servidor cheio fica bem abaixo; o teto existe para uma chave vazada
    ///     ou um mod com defeito não derrubar o TCMine.
    /// </summary>
    private const int CloudPermitLimit = 1200;

    /// <summary>
    ///     Tentativas de autenticação por janela, por IP.
    ///     Dez é folgado para quem erra a senha e aperta de novo, e apertado o
    ///     bastante para inviabilizar força bruta: dois palpites por minuto não
    ///     varrem dicionário nenhum.
    /// </summary>
    private const int AuthPermitLimit = 10;

    /// <summary>
    ///     Downloads simultâneos por IP.
    ///     Aqui o limite é de CONCORRÊNCIA, não de contagem: um launcher
    ///     sincronizando um modpack pede centenas de arquivos em sequência de
    ///     forma perfeitamente legítima, e um limite por contagem o mataria no
    ///     meio da instalação. O que precisa de teto é quantas transferências
    ///     pesadas a mesma origem segura abertas ao mesmo tempo.
    /// </summary>
    private const int BlobConcurrency = 8;

    /// <summary>
    ///     Excedentes esperam em vez de levar 429. Uma casa inteira atrás do
    ///     mesmo IP (CGNAT, NAT doméstico) compartilha a cota — enfileirar
    ///     atrasa a sincronização, rejeitar a quebraria.
    /// </summary>
    private const int BlobQueueLimit = 32;

    private static readonly TimeSpan CloudWindow = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan AuthWindow = TimeSpan.FromMinutes(5);

    public static IServiceCollection AddTcMineRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(AuthPolicy, http =>
                RateLimitPartition.GetFixedWindowLimiter(ClientKey(http), _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = AuthPermitLimit,
                        Window = AuthWindow,

                        // Sem fila de propósito: segurar uma tentativa de login
                        // para responder depois só ocupa recurso do servidor. Quem
                        // passou do limite tem de ouvir "não" agora.
                        QueueLimit = 0
                    }));

            options.AddPolicy(BlobPolicy, http =>
                RateLimitPartition.GetConcurrencyLimiter(ClientKey(http), _ =>
                    new ConcurrencyLimiterOptions
                    {
                        PermitLimit = BlobConcurrency,
                        QueueLimit = BlobQueueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    }));

            options.AddPolicy(CloudPolicy, http =>
                RateLimitPartition.GetFixedWindowLimiter(CloudKey(http), _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = CloudPermitLimit, Window = CloudWindow, QueueLimit = 0
                    }));

            options.OnRejected = OnRejectedAsync;
        });

        return services;
    }

    /// <summary>
    ///     Chave da partição: o IP do cliente.
    ///     Depende do UseForwardedHeaders ter rodado ANTES — sem ele, atrás de um
    ///     proxy reverso todo mundo compartilha o IP do proxy e o limite vira um
    ///     balde só para a internet inteira.
    ///     IP ausente cai num balde comum em vez de escapar do limite: preferimos
    ///     penalizar um caso raro a abrir uma brecha trivial.
    /// </summary>
    private static string ClientKey(HttpContext http) =>
        http.Connection.RemoteIpAddress?.ToString() ?? "sem-ip";

    private static ValueTask OnRejectedAsync(OnRejectedContext context, CancellationToken ct)
    {
        var http = context.HttpContext;

        const string mensagem =
            "Tentativas demais em pouco tempo. Espere alguns minutos e tente de novo.";

        // Post de formulário OU navegação de página inteira do painel (GET em
        // /auth/microsoft/*: o navegador segue o link direto, não é fetch/XHR)
        // vêm do navegador — um 429 cru seria uma página branca de erro nos
        // dois casos. O download de blob (BlobPolicy) também é GET, mas é o
        // launcher pedindo — ele espera um status, não um redirect para uma
        // tela de login que não existe para ele.
        var navegacaoDoPainel = HttpMethods.IsGet(http.Request.Method)
                                && http.Request.Path.StartsWithSegments("/auth/microsoft");

        if (http.Request.HasFormContentType || navegacaoDoPainel)
        {
            http.Response.Redirect($"{OriginPage(http.Request.Path)}?error={Uri.EscapeDataString(mensagem)}");
            return ValueTask.CompletedTask;
        }

        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        // Retry-After só existe na janela fixa; o limitador de concorrência não
        // sabe dizer quando vaga uma permissão.
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            http.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo);
        }

        return new ValueTask(http.Response.WriteAsync(mensagem, ct));
    }

    /// <summary>Tela que originou o pedido.</summary>
    private static string OriginPage(PathString path) =>
        path.StartsWithSegments("/auth/setup") ? "/admin/setup" : "/admin/login";

    /// <summary>
    ///     Partição da API da nuvem: o prefixo da chave (parte não secreta). Sem
    ///     chave reconhecível, cai no IP — quem chuta chaves é limitado pela origem.
    /// </summary>
    private static string CloudKey(HttpContext http)
    {
        var header = http.Request.Headers.Authorization.ToString();
        var prefix = header.StartsWith("Bearer ", StringComparison.Ordinal)
            ? CloudServerKey.PrefixOf(header["Bearer ".Length..].Trim())
            : null;
        return prefix is null ? "ip:" + ClientKey(http) : "key:" + prefix;
    }
}
