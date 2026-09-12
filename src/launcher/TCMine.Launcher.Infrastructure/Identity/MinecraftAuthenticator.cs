using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Infrastructure.Identity;

/// <summary>
///     Da conta Microsoft ao token do Minecraft, em três saltos: Xbox Live,
///     XSTS e Minecraft Services.
///     Está aqui, e não no projeto do Windows, porque nada nesta cadeia é do
///     Windows — é HTTPS com três serviços da Microsoft. O que é do Windows é o
///     degrau anterior, o MSAL, e ele entra por
///     <see cref="IMicrosoftTokenProvider" />. A divisão importa no dia do port:
///     em Linux escreve-se um provedor de token, não uma autenticação inteira.
///     Nenhum token passa pelo log. O que se registra é em que salto parou.
/// </summary>
public sealed partial class MinecraftAuthenticator(
    HttpClient http,
    IMicrosoftTokenProvider microsoft,
    ILogger<MinecraftAuthenticator> logger) : IMinecraftAuthenticator
{
    private static readonly Uri XboxLiveUrl = new("https://user.auth.xboxlive.com/user/authenticate");
    private static readonly Uri XstsUrl = new("https://xsts.auth.xboxlive.com/xsts/authorize");

    private static readonly Uri MinecraftUrl =
        new("https://api.minecraftservices.com/authentication/login_with_xbox");

    private readonly ILogger<MinecraftAuthenticator> _logger = logger;

    public async Task<AuthResult> TrySilentAsync(string azureClientId, CancellationToken ct) =>
        await ContinuarAsync(await microsoft.TrySilentAsync(azureClientId, ct), ct);

    public async Task<AuthResult> SignInAsync(string azureClientId, CancellationToken ct) =>
        await ContinuarAsync(await microsoft.SignInAsync(azureClientId, ct), ct);

    public Task SignOutAsync(CancellationToken ct) => microsoft.SignOutAsync(ct);

    /// <summary>
    ///     Só há o que fazer quando a Microsoft disse sim. Todo outro desfecho é
    ///     devolvido intacto de propósito: "não havia credencial guardada" e "o
    ///     jogador fechou a janela" já são respostas completas, e reembrulhá-las
    ///     como falha genérica apagaria a diferença que a tela usa para decidir
    ///     entre calar-se e mostrar um erro.
    /// </summary>
    private async Task<AuthResult> ContinuarAsync(AuthResult microsoftToken, CancellationToken ct)
    {
        if (microsoftToken.Outcome is not AuthOutcome.Success)
            return microsoftToken;

        try
        {
            var xboxLive = await AutenticarNoXboxLiveAsync(microsoftToken.AccessToken!, ct);

            if (xboxLive.Outcome is not AuthOutcome.Success)
                return xboxLive;

            var xsts = await AutorizarNoXstsAsync(xboxLive.AccessToken!, ct);

            if (xsts.Resultado.Outcome is not AuthOutcome.Success)
                return xsts.Resultado;

            // O user hash do XSTS é o que vale: o do Xbox Live é do mesmo
            // jogador, mas o Minecraft valida o par do token que recebe.
            return await EntrarNoMinecraftAsync(xsts.Resultado.AccessToken!, xsts.UserHash!, ct);
        }
        catch (HttpRequestException ex)
        {
            LogRedeIndisponivel(ex);

            return AuthResult.Failed(
                "Não foi possível falar com os serviços da Microsoft. Verifique a ligação e tente de novo.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return AuthResult.Failed("Os serviços da Microsoft demoraram demais para responder.");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // Formato inesperado é problema do outro lado, não do jogador — mas
            // ele precisa de uma saída, e "tente mais tarde" é a honesta.
            LogRespostaIlegivel(ex);
            return AuthResult.Failed("Os serviços da Microsoft responderam algo que não entendemos.");
        }
    }

    /// <summary>
    ///     O XSTS devolve duas coisas que só valem juntas, e o
    ///     <see cref="AuthResult" /> carrega uma só. Saem daqui em par, e não num
    ///     campo da classe: alargar o resultado contaminaria as duas portas que o
    ///     usam, e um campo seria estado partilhado entre logins — invisível
    ///     enquanto ninguém entra duas vezes ao mesmo tempo, e errado sempre.
    /// </summary>
    private sealed record SessaoXsts(AuthResult Resultado, string? UserHash = null);

    private async Task<AuthResult> AutenticarNoXboxLiveAsync(string microsoftToken, CancellationToken ct)
    {
        var pedido = new XboxAuthRequest
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

        var resposta = await http.PostAsJsonAsync(
            XboxLiveUrl, pedido, XboxAuthJsonContext.Default.XboxAuthRequest, ct);

        if (!resposta.IsSuccessStatusCode)
        {
            LogSaltoFalhou("Xbox Live", (int)resposta.StatusCode);

            return AuthResult.Failed(
                resposta.StatusCode is HttpStatusCode.Unauthorized
                    ? "O Xbox Live não aceitou esta conta Microsoft."
                    : $"O Xbox Live respondeu {(int)resposta.StatusCode}.");
        }

        var conteudo = await resposta.Content.ReadFromJsonAsync(
            XboxAuthJsonContext.Default.XboxAuthResponse, ct);

        return conteudo?.Token is { Length: > 0 } token
            ? AuthResult.Success(token)
            : AuthResult.Failed("O Xbox Live respondeu sem token.");
    }

    private async Task<SessaoXsts> AutorizarNoXstsAsync(string xboxLiveToken, CancellationToken ct)
    {
        var pedido = new XboxAuthRequest
        {
            RelyingParty = "rp://api.minecraftservices.com/",
            Properties = new XboxAuthProperties { SandboxId = "RETAIL", UserTokens = [xboxLiveToken] }
        };

        var resposta = await http.PostAsJsonAsync(
            XstsUrl, pedido, XboxAuthJsonContext.Default.XboxAuthRequest, ct);

        // O 401 daqui é o erro que o jogador mais vai encontrar, e é o único com
        // conserto do lado dele — desde que a mensagem diga qual conserto.
        if (resposta.StatusCode is HttpStatusCode.Unauthorized)
        {
            var erro = await resposta.Content.ReadFromJsonAsync(
                XboxAuthJsonContext.Default.XstsErrorResponse, ct);

            LogXstsRecusou(erro?.XErr ?? 0);

            return new SessaoXsts(AuthResult.Failed(MensagemDoXsts(erro?.XErr ?? 0)));
        }

        if (!resposta.IsSuccessStatusCode)
        {
            LogSaltoFalhou("XSTS", (int)resposta.StatusCode);
            return new SessaoXsts(AuthResult.Failed($"O XSTS respondeu {(int)resposta.StatusCode}."));
        }

        var conteudo = await resposta.Content.ReadFromJsonAsync(
            XboxAuthJsonContext.Default.XboxAuthResponse, ct);

        if (conteudo?.Token is not { Length: > 0 } token || conteudo.UserHash is not { Length: > 0 } hash)
        {
            return new SessaoXsts(
                AuthResult.Failed("O XSTS respondeu sem token ou sem identificação do jogador."));
        }

        return new SessaoXsts(AuthResult.Success(token), hash);
    }

    private async Task<AuthResult> EntrarNoMinecraftAsync(string xstsToken, string userHash, CancellationToken ct)
    {
        var pedido = new MinecraftLoginWithXboxRequest { IdentityToken = $"XBL3.0 x={userHash};{xstsToken}" };

        var resposta = await http.PostAsJsonAsync(
            MinecraftUrl, pedido, XboxAuthJsonContext.Default.MinecraftLoginWithXboxRequest, ct);

        if (!resposta.IsSuccessStatusCode)
        {
            LogSaltoFalhou("Minecraft Services", (int)resposta.StatusCode);
            return AuthResult.Failed($"O serviço do Minecraft respondeu {(int)resposta.StatusCode}.");
        }

        var conteudo = await resposta.Content.ReadFromJsonAsync(
            XboxAuthJsonContext.Default.MinecraftLoginWithXboxResponse, ct);

        // Não verificamos aqui se a conta tem o jogo: quem decide quem entra é o
        // servidor, que consulta o perfil na Mojang. Duplicar a verificação daria
        // dois lugares para a regra mudar e um deles ficar para trás.
        return conteudo?.AccessToken is { Length: > 0 } token
            ? AuthResult.Success(token)
            : AuthResult.Failed("O serviço do Minecraft respondeu sem token.");
    }

    /// <summary>
    ///     Os códigos que o XSTS devolve no 401. Traduzi-los é o que separa "não
    ///     foi possível entrar" de uma instrução acionável — e três destes quatro
    ///     casos o jogador resolve sozinho em cinco minutos.
    /// </summary>
    private static string MensagemDoXsts(long xerr) => xerr switch
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Autenticação parou no salto {Salto} com HTTP {Codigo}.")]
    private partial void LogSaltoFalhou(string salto, int codigo);

    [LoggerMessage(Level = LogLevel.Warning, Message = "XSTS recusou a conta com XErr {XErr}.")]
    private partial void LogXstsRecusou(long xErr);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Serviços da Microsoft inalcançáveis na autenticação.")]
    private partial void LogRedeIndisponivel(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Resposta ilegível na cadeia de autenticação.")]
    private partial void LogRespostaIlegivel(Exception ex);
}
