using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TCMine.Contracts.Identity;
using TCMine.Contracts.Serialization;
using TCMine.Launcher.Core.Identity;

namespace TCMine.Launcher.Infrastructure.Identity;

/// <summary>
///     Troca o token do Minecraft por uma sessão no TCMine Server.
///     O cookie devolvido não é lido aqui: ele fica no <see cref="CookieContainer" />
///     compartilhado do handler, e é isso que faz o hub e os downloads
///     autenticarem depois sem ninguém passar credencial adiante. É a mesma
///     sessão do painel — não existe caminho paralelo de autenticação.
/// </summary>
public sealed partial class LauncherSessionApi(
    HttpClient http,
    ILogger<LauncherSessionApi> logger) : ILauncherSessionApi
{
    private readonly ILogger<LauncherSessionApi> _logger = logger;

    public async Task<SessionResult> SignInAsync(Uri serverUrl, string minecraftAccessToken, CancellationToken ct)
    {
        var endpoint = new Uri(serverUrl, "/api/v1/auth/minecraft");

        try
        {
            var response = await http.PostAsJsonAsync(
                endpoint,
                new MinecraftLoginRequest { AccessToken = minecraftAccessToken },
                TcMineJsonContext.Default.MinecraftLoginRequest,
                ct);

            // 401 é o servidor dizendo que a credencial não serve; qualquer
            // outro código é problema de infraestrutura. A distinção decide se a
            // interface oferece "tentar de novo" ou manda trocar de conta.
            if (response.StatusCode is HttpStatusCode.Unauthorized)
            {
                LogRecusado(endpoint);

                return SessionResult.Rejected(
                    "O servidor não reconheceu esta conta Minecraft. Verifique se é a conta certa.");
            }

            if (!response.IsSuccessStatusCode)
            {
                LogFailed(endpoint, (int)response.StatusCode);
                return SessionResult.Failed($"O servidor respondeu {(int)response.StatusCode} ao entrar.");
            }

            var session = await response.Content.ReadFromJsonAsync(
                TcMineJsonContext.Default.LauncherSessionDto, ct);

            return session is null
                ? SessionResult.Failed("O servidor aceitou a conta mas não devolveu a sessão.")
                : SessionResult.Success(session);
        }
        catch (HttpRequestException ex)
        {
            LogErro(ex, endpoint);
            return SessionResult.Failed("Não foi possível alcançar o servidor para entrar.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return SessionResult.Failed("O servidor demorou demais para responder.");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            LogErro(ex, endpoint);
            return SessionResult.Failed("A resposta do servidor não pôde ser interpretada.");
        }
    }

    public async Task<InviteRedeemResult> RedeemInviteAsync(Uri serverUrl, string code, CancellationToken ct)
    {
        var endpoint = new Uri(serverUrl, "/api/v1/invites/redeem");

        try
        {
            var response = await http.PostAsJsonAsync(
                endpoint,
                new RedeemInviteRequest { Code = code },
                TcMineJsonContext.Default.RedeemInviteRequest,
                ct);

            if (response.IsSuccessStatusCode)
                return InviteRedeemResult.Success();

            // Results.Problem devolve application/problem+json; a mensagem que
            // o jogador precisa ver — "Convite inválido ou expirado." — está no
            // campo "detail", não no corpo inteiro. JsonDocument porque isto é
            // o único lugar que lê este formato: não vale um tipo no contexto
            // de source-gen só por causa dele.
            LogFailed(endpoint, (int)response.StatusCode);
            var detail = await TryReadProblemDetailAsync(response, ct);

            return InviteRedeemResult.Failed(detail ?? "Não foi possível resgatar o convite.");
        }
        catch (HttpRequestException ex)
        {
            LogErro(ex, endpoint);
            return InviteRedeemResult.Failed("Não foi possível alcançar o servidor para resgatar o convite.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return InviteRedeemResult.Failed("O servidor demorou demais para responder.");
        }
    }

    public async Task SignOutAsync(Uri serverUrl, CancellationToken ct)
    {
        try
        {
            await http.PostAsync(new Uri(serverUrl, "/api/v1/auth/logout"), null, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Sair local não pode depender do servidor estar acessível: quem
            // pediu para sair tem de sair, e a sessão do outro lado expira
            // sozinha. Registrar basta.
            LogSaidaSemServidor(ex);
        }
    }

    private static async Task<string?> TryReadProblemDetailAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var document =
                await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

            return document.RootElement.TryGetProperty("detail", out var detail)
                ? detail.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "O servidor recusou a conta em {Endpoint}.")]
    private partial void LogRecusado(Uri endpoint);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Entrada em {Endpoint} respondeu {StatusCode}.")]
    private partial void LogFailed(Uri endpoint, int statusCode);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao falar com {Endpoint}.")]
    private partial void LogErro(Exception ex, Uri endpoint);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Saída local feita sem alcançar o servidor.")]
    private partial void LogSaidaSemServidor(Exception ex);
}
