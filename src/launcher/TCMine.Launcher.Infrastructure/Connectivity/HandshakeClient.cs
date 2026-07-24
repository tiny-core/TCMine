using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TCMine.Contracts;
using TCMine.Contracts.Handshake;
using TCMine.Contracts.Serialization;
using TCMine.Launcher.Core.Connectivity;

namespace TCMine.Launcher.Infrastructure.Connectivity;

/// <summary>
///     Primeira chamada ao servidor, antes de qualquer outra coisa.
///     Toda falha aqui vira um HandshakeOutcome com mensagem legível, nunca uma
///     exceção que sobe até a UI. O jogador precisa entender o que fazer — e
///     "servidor incompatível" e "sem internet" pedem ações diferentes.
/// </summary>
public sealed partial class HandshakeClient(
    HttpClient http,
    ILogger<HandshakeClient> logger) : IHandshakeClient
{
    private readonly ILogger<HandshakeClient> _logger = logger;

    public async Task<HandshakeResult> PerformAsync(Uri serverUrl, CancellationToken ct)
    {
        var endpoint = new Uri(serverUrl, Protocol.HandshakeRoute);

        try
        {
            var response = await http.GetAsync(endpoint, ct);

            if (!response.IsSuccessStatusCode)
            {
                LogHandshakeFailed(endpoint, (int)response.StatusCode);

                return new HandshakeResult(
                    HandshakeOutcome.Unreachable,
                    null,
                    $"O servidor respondeu {(int)response.StatusCode}. Verifique o endereço.");
            }

            var handshake = await response.Content.ReadFromJsonAsync(
                TcMineJsonContext.Default.HandshakeResponse, ct);

            if (handshake is null)
                return new HandshakeResult(
                    HandshakeOutcome.InvalidResponse,
                    null,
                    "O endereço respondeu, mas não parece ser um servidor TCMine.");

            return Evaluate(handshake);
        }
        catch (HttpRequestException ex)
        {
            LogHandshakeError(ex, endpoint);

            return new HandshakeResult(
                HandshakeOutcome.Unreachable,
                null,
                "Não foi possível alcançar o servidor. Verifique sua conexão e o endereço.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            // Cancelamento por timeout, não pelo usuário fechar a janela.
            return new HandshakeResult(
                HandshakeOutcome.Unreachable,
                null,
                "O servidor demorou demais para responder.");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            LogHandshakeError(ex, endpoint);

            return new HandshakeResult(
                HandshakeOutcome.InvalidResponse,
                null,
                "A resposta do servidor não pôde ser interpretada.");
        }
    }

    /// <summary>
    ///     Compara os intervalos de protocolo dos dois lados.
    ///     A distinção entre "launcher antigo" e "launcher novo demais" importa
    ///     para a mensagem: no primeiro caso o jogador atualiza, no segundo é o
    ///     admin do servidor que precisa agir.
    /// </summary>
    private static HandshakeResult Evaluate(HandshakeResponse handshake)
    {
        if (Protocol.IsCompatible(handshake.ProtocolMin, handshake.ProtocolMax))
            return new HandshakeResult(HandshakeOutcome.Ok, handshake, null);

        if (handshake.ProtocolMin > Protocol.Current)
            return new HandshakeResult(
                HandshakeOutcome.LauncherTooOld,
                handshake,
                $"Seu launcher está desatualizado para o servidor {handshake.ServerName}. " +
                "Baixe a versão nova na página do servidor.");

        return new HandshakeResult(
            HandshakeOutcome.LauncherTooNew,
            handshake,
            $"O servidor {handshake.ServerName} está rodando uma versão antiga do TCMine. " +
            "Avise o administrador.");
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Handshake com {Endpoint} respondeu {StatusCode}.")]
    private partial void LogHandshakeFailed(Uri endpoint, int statusCode);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha no handshake com {Endpoint}.")]
    private partial void LogHandshakeError(Exception ex, Uri endpoint);
}