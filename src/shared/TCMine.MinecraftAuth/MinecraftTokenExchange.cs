using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TCMine.MinecraftAuth;

/// <summary>
///     De um token Microsoft (escopo <c>XboxLive.signin</c>) a um token do
///     Minecraft, em três saltos: Xbox Live, XSTS e Minecraft Services.
///     Portável de propósito: nada aqui sabe COMO se chegou ao token Microsoft
///     — o launcher chega via MSAL e o broker do Windows, o painel web chega
///     via redirect de navegador comum — só o que fazer depois de o ter em
///     mãos. Nenhum token passa pelo log; o que se registra é em que salto
///     parou.
/// </summary>
public sealed partial class MinecraftTokenExchange(
    HttpClient http,
    ILogger<MinecraftTokenExchange> logger)
{
    private static readonly Uri XboxLiveUrl = new("https://user.auth.xboxlive.com/user/authenticate");
    private static readonly Uri XstsUrl = new("https://xsts.auth.xboxlive.com/xsts/authorize");

    private static readonly Uri MinecraftUrl =
        new("https://api.minecraftservices.com/authentication/login_with_xbox");

    private readonly ILogger<MinecraftTokenExchange> _logger = logger;

    public async Task<MinecraftTokenResult> ExchangeAsync(string microsoftAccessToken, CancellationToken ct)
    {
        try
        {
            var xboxLive = await AuthenticateXboxLiveAsync(microsoftAccessToken, ct);

            if (xboxLive.Outcome is not MinecraftTokenOutcome.Success)
                return xboxLive;

            var xsts = await AuthorizeXstsAsync(xboxLive.AccessToken!, ct);

            if (xsts.Resultado.Outcome is not MinecraftTokenOutcome.Success)
                return xsts.Resultado;

            // O user hash do XSTS é o que vale: o do Xbox Live é do mesmo
            // jogador, mas o Minecraft valida o par do token que recebe.
            return await SignInToMinecraftAsync(xsts.Resultado.AccessToken!, xsts.UserHash!, ct);
        }
        catch (HttpRequestException ex)
        {
            LogNetworkDown(ex);

            return MinecraftTokenResult.Failed(
                "Não foi possível falar com os serviços da Microsoft. Verifique a ligação e tente de novo.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return MinecraftTokenResult.Failed("Os serviços da Microsoft demoraram demais para responder.");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // Formato inesperado é problema do outro lado, não de quem chamou —
            // mas precisa de uma saída, e "tente mais tarde" é a honesta.
            LogUnreadableResponse(ex);
            return MinecraftTokenResult.Failed("Os serviços da Microsoft responderam algo que não entendemos.");
        }
    }

    private async Task<MinecraftTokenResult> AuthenticateXboxLiveAsync(string microsoftToken, CancellationToken ct)
    {
        var request = new XboxAuthRequest
        {
            RelyingParty = "http://auth.xboxlive.com",
            Properties = new XboxAuthProperties
            {
                AuthMethod = "RPS",
                SiteName = "user.auth.xboxlive.com",

                // O "d=" marca o ticket como vindo do Microsoft identity
                // platform. Sem o prefixo, o Xbox Live responde 400 sem dizer
                // por quê — e o token está correto.
                RpsTicket = $"d={microsoftToken}"
            }
        };

        var response = await http.PostAsJsonAsync(
            XboxLiveUrl, request, XboxAuthJsonContext.Default.XboxAuthRequest, ct);

        if (!response.IsSuccessStatusCode)
        {
            LogHopFailed("Xbox Live", (int)response.StatusCode);

            return MinecraftTokenResult.Failed(
                response.StatusCode is HttpStatusCode.Unauthorized
                    ? "O Xbox Live não aceitou esta conta Microsoft."
                    : $"O Xbox Live respondeu {(int)response.StatusCode}.");
        }

        var content = await response.Content.ReadFromJsonAsync(
            XboxAuthJsonContext.Default.XboxAuthResponse, ct);

        return content?.Token is { Length: > 0 } token
            ? MinecraftTokenResult.Success(token)
            : MinecraftTokenResult.Failed("O Xbox Live respondeu sem token.");
    }

    private async Task<XstsSession> AuthorizeXstsAsync(string xboxLiveToken, CancellationToken ct)
    {
        var request = new XboxAuthRequest
        {
            RelyingParty = "rp://api.minecraftservices.com/",
            Properties = new XboxAuthProperties { SandboxId = "RETAIL", UserTokens = [xboxLiveToken] }
        };

        var response = await http.PostAsJsonAsync(
            XstsUrl, request, XboxAuthJsonContext.Default.XboxAuthRequest, ct);

        // O 401 daqui é o erro que mais aparece, e é o único com conserto do
        // lado de quem está autenticando — desde que a mensagem diga qual.
        if (response.StatusCode is HttpStatusCode.Unauthorized)
        {
            var error = await response.Content.ReadFromJsonAsync(
                XboxAuthJsonContext.Default.XstsErrorResponse, ct);

            LogXstsRejected(error?.XErr ?? 0);

            return new XstsSession(MinecraftTokenResult.Failed(XstsMessage(error?.XErr ?? 0)));
        }

        if (!response.IsSuccessStatusCode)
        {
            LogHopFailed("XSTS", (int)response.StatusCode);
            return new XstsSession(MinecraftTokenResult.Failed($"O XSTS respondeu {(int)response.StatusCode}."));
        }

        var content = await response.Content.ReadFromJsonAsync(
            XboxAuthJsonContext.Default.XboxAuthResponse, ct);

        if (content?.Token is not { Length: > 0 } token || content.UserHash is not { Length: > 0 } hash)
        {
            return new XstsSession(
                MinecraftTokenResult.Failed("O XSTS respondeu sem token ou sem identificação do jogador."));
        }

        return new XstsSession(MinecraftTokenResult.Success(token), hash);
    }

    private async Task<MinecraftTokenResult> SignInToMinecraftAsync(
        string xstsToken, string userHash, CancellationToken ct)
    {
        var request = new MinecraftLoginWithXboxRequest { IdentityToken = $"XBL3.0 x={userHash};{xstsToken}" };

        var response = await http.PostAsJsonAsync(
            MinecraftUrl, request, XboxAuthJsonContext.Default.MinecraftLoginWithXboxRequest, ct);

        if (!response.IsSuccessStatusCode)
        {
            LogHopFailed("Minecraft Services", (int)response.StatusCode);
            return MinecraftTokenResult.Failed($"O serviço do Minecraft respondeu {(int)response.StatusCode}.");
        }

        var content = await response.Content.ReadFromJsonAsync(
            XboxAuthJsonContext.Default.MinecraftLoginWithXboxResponse, ct);

        // Não verificamos aqui se a conta tem o jogo: isso é o perfil
        // (GET /minecraft/profile, outra chamada), não este token. Duplicar a
        // verificação daria dois lugares para a regra mudar e um deles ficar
        // para trás.
        return content?.AccessToken is { Length: > 0 } token
            ? MinecraftTokenResult.Success(token)
            : MinecraftTokenResult.Failed("O serviço do Minecraft respondeu sem token.");
    }

    /// <summary>
    ///     Os códigos que o XSTS devolve no 401. Traduzi-los é o que separa "não
    ///     foi possível entrar" de uma instrução acionável — e três destes quatro
    ///     casos resolvem-se sozinhos em cinco minutos.
    /// </summary>
    private static string XstsMessage(long xerr) => xerr switch
    {
        2148916233 => "Esta conta Microsoft não tem um perfil do Xbox. Crie um em xbox.com com a "
                      + "mesma conta e tente de novo.",
        2148916235 => "O Xbox Live não está disponível no país desta conta.",
        2148916236 or 2148916237 => "Esta conta precisa completar a verificação de idade para usar o Xbox Live.",
        2148916238 => "Esta é uma conta de menor: um responsável precisa adicioná-la a uma família "
                      + "antes de ela poder entrar.",
        0 => "O Xbox Live recusou esta conta.",
        _ => $"O Xbox Live recusou esta conta (erro {xerr})."
    };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Troca de token parou no salto {Hop} com HTTP {Code}.")]
    private partial void LogHopFailed(string hop, int code);

    [LoggerMessage(Level = LogLevel.Warning, Message = "XSTS recusou a conta com XErr {XErr}.")]
    private partial void LogXstsRejected(long xErr);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Serviços da Microsoft inalcançáveis na troca de token.")]
    private partial void LogNetworkDown(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Resposta ilegível na troca de token.")]
    private partial void LogUnreadableResponse(Exception ex);

    /// <summary>
    ///     O XSTS devolve duas coisas que só valem juntas, e o
    ///     <see cref="MinecraftTokenResult" /> carrega uma só. Saem daqui em par,
    ///     e não num campo da classe: alargar o resultado contaminaria as duas
    ///     portas que o usam, e um campo seria estado partilhado entre trocas —
    ///     invisível enquanto ninguém troca duas ao mesmo tempo, e errado sempre.
    /// </summary>
    private sealed record XstsSession(MinecraftTokenResult Resultado, string? UserHash = null);
}
