using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace TCMine_Server.Infrastructure.Minecraft;

/// <summary>
///     Valida o access token do Minecraft (MSession) contra a API de perfil da Mojang e confirma que
///     pertence ao UUID indicado. Usado para autenticar a <b>escrita</b> das configs do jogador (PUT).
///     <b>Fail-open</b>: se a Mojang estiver inacessível, autoriza — são settings de jogo, sem
///     segredos, e não adianta partir o sync por uma indisponibilidade externa. Só NEGA quando o
///     token é confirmadamente inválido (401/403) ou o UUID não corresponde. Resultados cacheados
///     em memória (~10 min) para não bater na Mojang a cada PUT.
/// </summary>
public sealed class MinecraftAuthService(IHttpClientFactory http, IMemoryCache cache, ILogger<MinecraftAuthService> log)
{
    private const string ProfileUrl = "https://api.minecraftservices.com/minecraft/profile";

    public async Task<bool> AuthorizeAsync(string token, string expectedUuid, CancellationToken ct)
    {
        var want = Normalize(expectedUuid);
        if (string.IsNullOrEmpty(token) || want is null) return false;

        var key = "mcauth:" + token;
        if (cache.TryGetValue(key, out string? cachedUuid))
            return cachedUuid is not null && cachedUuid == want;

        try
        {
            var client = http.CreateClient();
            using var req = new HttpRequestMessage(HttpMethod.Get, ProfileUrl);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var resp = await client.SendAsync(req, ct);

            if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                cache.Set<string?>(key, null, TimeSpan.FromMinutes(1)); // inválido confirmado
                return false;
            }

            if (!resp.IsSuccessStatusCode)
            {
                log.LogWarning("Mojang devolveu {Status} ao validar token — fail-open", (int)resp.StatusCode);
                return true; // indisponível → não bloqueia o sync
            }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var id = doc.RootElement.TryGetProperty("id", out var v) ? Normalize(v.GetString()) : null;
            cache.Set(key, id, TimeSpan.FromMinutes(10));
            return id is not null && id == want;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Falha a validar token na Mojang — fail-open");
            return true; // erro de rede → não bloqueia o sync
        }
    }

    /// <summary>UUID em minúsculas e sem hífens (formato consistente para comparação).</summary>
    private static string? Normalize(string? uuid)
    {
        return string.IsNullOrWhiteSpace(uuid) ? null : uuid.Replace("-", "").Trim().ToLowerInvariant();
    }
}