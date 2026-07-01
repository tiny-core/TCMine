using TCMine_Application.Abstractions;
using TCMine_Server.Infrastructure.Minecraft;

namespace TCMine_Server.Endpoints;

/// <summary>
/// Sincronização das configs do jogador entre PCs, por <c>(uuid, modpackId)</c>, como um zip.
///
/// A <b>leitura</b> (GET) é aberta — são settings de jogo, sem segredos. A <b>escrita</b> (PUT)
/// exige um access token Minecraft válido (header <c>Authorization: Bearer…</c>) que pertença ao
/// UUID, validado contra a Mojang (ver <see cref="MinecraftAuthService"/>). O PUT é limitado por
/// taxa (política "configs") para conter abuso.
/// </summary>
public static class PlayerConfigEndpoints
{
    // Limite defensivo do corpo do PUT — configs de jogo não passam disto.
    private const long MaxConfigBytes = 25 * 1024 * 1024; // 25 MB

    public static void MapPlayerConfigEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPut("/players/{uuid}/configs/{modpackId}", async (
            string uuid, string modpackId, HttpContext ctx, IPlayerConfigRepository store,
            MinecraftAuthService auth, CancellationToken ct) =>
        {
            if (!IsValidKey(uuid) || !IsValidKey(modpackId)) return Results.BadRequest();

            // Escrita exige um token Minecraft válido que pertença a este UUID.
            var token = BearerToken(ctx);
            if (token is null) return Results.Unauthorized();
            if (!await auth.AuthorizeAsync(token, uuid, ct)) return Results.StatusCode(403);

            using var ms = new MemoryStream();
            await ctx.Request.Body.CopyToAsync(ms, ct);
            if (ms.Length is 0 or > MaxConfigBytes) return Results.BadRequest();

            var updatedAt = await store.UpsertAsync(uuid, modpackId, ms.ToArray(), ct);
            return Results.Json(new { updatedAt });
        }).RequireRateLimiting("configs");
    }

    /// <summary>Aceita só chaves simples (defesa, embora sejam apenas chaves de BD).</summary>
    private static bool IsValidKey(string s)
    {
        return !string.IsNullOrWhiteSpace(s) && s.Length <= 80 &&
               s.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');
    }

    /// <summary>Extrai o token de "Authorization: Bearer &lt;token&gt;".</summary>
    private static string? BearerToken(HttpContext ctx)
    {
        var h = ctx.Request.Headers.Authorization.ToString();
        return h.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? h["Bearer ".Length..].Trim()
            : null;
    }
}