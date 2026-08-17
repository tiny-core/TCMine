using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Infrastructure.Security;

/// <summary>
///     Verifica o token do jogador contra os serviços da Mojang.
///     O endpoint de perfil é o mais barato que responde às duas perguntas de
///     uma vez: ele exige um token válido (autenticidade) e só existe para quem
///     possui o jogo (propriedade). Uma conta Microsoft sem Minecraft recebe 404
///     aqui, e é assim que ela é recusada.
/// </summary>
public sealed partial class MinecraftServicesProfileSource(
    HttpClient http,
    ILogger<MinecraftServicesProfileSource> logger) : IMinecraftProfileSource
{
    public async Task<MinecraftProfile?> GetProfileAsync(string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/minecraft/profile");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await http.SendAsync(request, ct);

        // 401 é token inválido ou expirado; 404 é conta sem o jogo. Os dois são
        // "não entra", e a distinção não volta ao cliente de propósito: dizer
        // qual dos dois falhou ajuda quem está testando tokens roubados.
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            PerfilRecusado((int)response.StatusCode);
            return null;
        }

        // Qualquer outro status é problema NOSSO ou da Mojang, não do jogador.
        // Deixar subir é o certo: transformar indisponibilidade em "token
        // inválido" tiraria todo mundo do ar sem deixar rastro do motivo.
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ProfileResponse>(ct);

        if (payload is null || string.IsNullOrWhiteSpace(payload.Id) || string.IsNullOrWhiteSpace(payload.Name))
        {
            PerfilIncompleto();
            return null;
        }

        // Normaliza aqui, na borda: o resto do sistema assume UUID minúsculo e
        // sem hífens, e é este o único ponto por onde ele entra.
        var uuid = payload.Id.Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();

        return new MinecraftProfile(uuid, payload.Name);
    }

    [LoggerMessage(LogLevel.Information, "Perfil Minecraft recusado pela Mojang (HTTP {Status}).")]
    private partial void PerfilRecusado(int status);

    [LoggerMessage(LogLevel.Warning, "Mojang respondeu 200 com perfil sem id ou nome.")]
    private partial void PerfilIncompleto();

    private sealed record ProfileResponse(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name);
}
