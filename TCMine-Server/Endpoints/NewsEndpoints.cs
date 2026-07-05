using TCMine_Server.Infrastructure.Server;

namespace TCMine_Server.Endpoints;

/// <summary>
///     Feed público de novidades consumido pelo <b>launcher</b>: globais + de modpacks, só as publicadas.
///     Leitura pública (sem auth), como <c>/api/modpacks</c> — não há segredos no fluxo.
/// </summary>
public static class NewsEndpoints
{
    public static void MapNewsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/news", async (ModpackNewsService news, CancellationToken ct) =>
            Results.Ok(await news.ListPublishedAsync(ct)));
    }
}