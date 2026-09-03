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
            var resposta = await http.PostAsJsonAsync(
                endpoint,
                new MinecraftLoginRequest { AccessToken = minecraftAccessToken },
                TcMineJsonContext.Default.MinecraftLoginRequest,
                ct);

            // 401 é o servidor dizendo que a credencial não serve; qualquer
            // outro código é problema de infraestrutura. A distinção decide se a
            // interface oferece "tentar de novo" ou manda trocar de conta.
            if (resposta.StatusCode is HttpStatusCode.Unauthorized)
            {
                LogRecusado(endpoint);

                return SessionResult.Rejected(
                    "O servidor não reconheceu esta conta Minecraft. Verifique se é a conta certa.");
            }

            if (!resposta.IsSuccessStatusCode)
            {
                LogFalhou(endpoint, (int)resposta.StatusCode);
                return SessionResult.Failed($"O servidor respondeu {(int)resposta.StatusCode} ao entrar.");
            }

            var sessao = await resposta.Content.ReadFromJsonAsync(
                TcMineJsonContext.Default.LauncherSessionDto, ct);

            return sessao is null
                ? SessionResult.Failed("O servidor aceitou a conta mas não devolveu a sessão.")
                : SessionResult.Success(sessao);
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "O servidor recusou a conta em {Endpoint}.")]
    private partial void LogRecusado(Uri endpoint);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Entrada em {Endpoint} respondeu {StatusCode}.")]
    private partial void LogFalhou(Uri endpoint, int statusCode);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao falar com {Endpoint}.")]
    private partial void LogErro(Exception ex, Uri endpoint);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Saída local feita sem alcançar o servidor.")]
    private partial void LogSaidaSemServidor(Exception ex);
}
