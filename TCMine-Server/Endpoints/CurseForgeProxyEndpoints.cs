using TCMine_Server.Infrastructure.CurseForge;
using TCMine_Server.Infrastructure.Server;

namespace TCMine_Server.Endpoints;

/// <summary>
/// Proxy transparente para a API do CurseForge em <c>/v1/*</c>. O launcher pesquisa/resolve mods
/// (UI de adição manual) através daqui, e o servidor injeta a <c>x-api-key</c> — assim a key nunca
/// sai do servidor (ver CLAUDE.md: "CurseForge sempre via proxy do servidor").
///
/// É um passthrough genérico: encaminha método, query e corpo para <c>api.curseforge.com/v1/...</c>
/// e devolve a resposta como veio. Sem cache nem transformação — quem entende o JSON é o cliente.
/// </summary>
public static class CurseForgeProxyEndpoints
{
    public static void MapCurseForgeProxy(this IEndpointRouteBuilder app)
    {
        // Catch-all: tudo sob /v1 é repassado preservando o sufixo do caminho
        app.MapMethods("/v1/{**path}", ["GET", "POST"], async (
            string path, HttpContext ctx, IHttpClientFactory factory,
            ServerSettingsService settings, CancellationToken ct) =>
        {
            var apiKey = await settings.GetCfApiKeyAsync(ct);
            if (string.IsNullOrWhiteSpace(apiKey))
                // 503: dependência não configurada (Owner ainda não definiu o token)
                return Results.Problem("Token do CurseForge não configurado.", statusCode: 503);

            // Reconstrói o alvo: v1/<path><?query>, relativo à base do HttpClient nomeado
            var target = $"v1/{path}{ctx.Request.QueryString}";
            using var req = new HttpRequestMessage(new HttpMethod(ctx.Request.Method), target);
            req.Headers.Add("x-api-key", apiKey);

            // Repassa o corpo nas requisições com payload (ex.: POST /v1/mods em lote)
            if (ctx.Request.ContentLength > 0)
            {
                req.Content = new StreamContent(ctx.Request.Body);
                if (!string.IsNullOrEmpty(ctx.Request.ContentType))
                    req.Content.Headers.TryAddWithoutValidation("Content-Type", ctx.Request.ContentType);
            }

            var http = factory.CreateClient(CurseForgeApiClient.HttpClientName);
            using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

            // Espelha status, content-type e corpo do upstream (um 404 do CF chega como 404 aqui)
            ctx.Response.StatusCode = (int)resp.StatusCode;
            ctx.Response.ContentType = resp.Content.Headers.ContentType?.ToString() ?? "application/json";
            await resp.Content.CopyToAsync(ctx.Response.Body, ct);
            return Results.Empty;
        });
    }
}