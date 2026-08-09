using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Infrastructure.Ingestion.CurseForge;

/// <summary>
///     Cliente HTTP do CurseForge.
///     A chave de API vem da configuração da instalação (banco) e é lida a cada
///     chamada, não fixada no HttpClient: o admin pode trocá-la pelo painel sem
///     reiniciar o servidor.
/// </summary>
public sealed class CurseForgeApiClient(HttpClient http, ISettingsRepository settings)
{
    /// <summary>Id do Minecraft no catálogo do CurseForge.</summary>
    internal const int MinecraftGameId = 432;

    internal async Task<bool> HasApiKeyAsync(CancellationToken ct) =>
        !string.IsNullOrWhiteSpace(await settings.GetCurseForgeApiKeyAsync(ct));

    /// <summary>
    ///     GET autenticado. Devolve default quando não há chave, quando o recurso
    ///     não existe ou quando a API recusa — quem chama trata como "não achei",
    ///     porque nenhum desses casos deve derrubar uma ingestão inteira.
    /// </summary>
    internal async Task<T?> GetAsync<T>(string url, JsonTypeInfo<T> typeInfo, CancellationToken ct)
    {
        var apiKey = await settings.GetCurseForgeApiKeyAsync(ct);
        if (string.IsNullOrWhiteSpace(apiKey))
            return default;

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("x-api-key", apiKey);

        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized
            or HttpStatusCode.Forbidden)
            return default;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(typeInfo, ct);
    }

    /// <summary>
    ///     POST com corpo JSON. Existe para o endpoint /v1/mods em lote: pedir o
    ///     nome de 480 mods um a um seriam 480 chamadas e estouraria a cota — em
    ///     lote é uma só.
    /// </summary>
    internal async Task<TResponse?> PostAsync<TRequest, TResponse>(
        string url, TRequest body, JsonTypeInfo<TRequest> requestInfo, JsonTypeInfo<TResponse> responseInfo,
        CancellationToken ct)
    {
        var apiKey = await settings.GetCurseForgeApiKeyAsync(ct);
        if (string.IsNullOrWhiteSpace(apiKey))
            return default;

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("x-api-key", apiKey);
        request.Content = JsonContent.Create(body, requestInfo);

        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized
            or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest)
            return default;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(responseInfo, ct);
    }

    /// <summary>
    ///     O CurseForge identifica o loader por número. "Any" (0) serviria, mas
    ///     traz arquivos de loaders incompatíveis — sempre filtramos.
    /// </summary>
    internal static int ToLoaderType(ModLoader loader) => loader switch
    {
        ModLoader.Forge => 1,
        ModLoader.Fabric => 4,
        ModLoader.Quilt => 5,
        ModLoader.NeoForge => 6,
        _ => 0 // Vanilla: sem filtro de loader
    };
}
