using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Infrastructure.Identity;

/// <summary>
///     O perfil, direto do Minecraft Services.
///     É a fonte autoritativa: o nome que aparece no jogo e o UUID que identifica
///     o mundo do jogador saem daqui, não do servidor TCMine. O nosso servidor
///     continua a validar o mesmo token do lado dele — são verificações
///     independentes da mesma conta, e é assim que deve ser.
/// </summary>
public sealed partial class MinecraftServicesProfileSource(
    HttpClient http,
    ILogger<MinecraftServicesProfileSource> logger) : IPlayerProfileSource
{
    private static readonly Uri ProfileUrl = new("https://api.minecraftservices.com/minecraft/profile");

    private readonly ILogger<MinecraftServicesProfileSource> _logger = logger;

    public async Task<PlayerProfile?> GetAsync(string accessToken, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ProfileUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await http.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                // 404 aqui é a conta não ter o jogo — Microsoft válida, Minecraft
                // não comprado. Distinto de rede em baixo, e o jogador precisa de
                // saber a diferença, mas a decisão do que dizer é de quem chama.
                LogRejected((int)response.StatusCode);
                return null;
            }

            var perfil = await response.Content.ReadFromJsonAsync(
                MinecraftProfileJsonContext.Default.MinecraftProfileResponse, ct);

            return perfil?.Name is { Length: > 0 } name && perfil.Id is { Length: > 0 } id
                ? new PlayerProfile(name, id)
                : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Sem rede é o caso normal deste método, não uma falha: quem chama
            // recorre ao perfil guardado.
            LogUnavailable(ex);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "O Minecraft recusou o perfil com HTTP {Code}.")]
    private partial void LogRejected(int code);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Perfil do Minecraft indisponível; o launcher usa o último conhecido.")]
    private partial void LogUnavailable(Exception ex);
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(MinecraftProfileResponse))]
internal sealed partial class MinecraftProfileJsonContext : JsonSerializerContext;

internal sealed record MinecraftProfileResponse
{
    /// <summary>UUID minúsculo e sem hífens, como o Minecraft o devolve.</summary>
    public string? Id { get; init; }

    public string? Name { get; init; }
}
