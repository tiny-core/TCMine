using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Web.Configuration;

namespace TCMine.Server.Web.Endpoints;

/// <summary>
///     Download dos arquivos de modpack.
///     Nunca passe arquivo pelo SignalR: ele mantém tudo em memória e serializa
///     mensagem por mensagem. Download é HTTP, com Range request para resumir de
///     onde parou.
/// </summary>
public static class BlobEndpoints
{
    public static IEndpointRouteBuilder MapBlobs(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/blobs/{sha256}", async (
                string sha256,
                IBlobStore store,
                CancellationToken ct) =>
            {
                // O hash vem da URL, então é entrada de cliente: formato inválido
                // é erro DELE (400), não nosso (500). O store valida e lança.
                try
                {
                    if (!await store.ExistsAsync(sha256, ct))
                        return Results.NotFound();
                }
                catch (ArgumentException)
                {
                    return Results.BadRequest();
                }

                // Se o backend souber gerar URL pré-assinada (object storage),
                // redireciona e os bytes nem passam por este processo.
                var direct = await store.TryGetDirectUrlAsync(sha256, TimeSpan.FromMinutes(15), ct);
                if (direct is not null)
                    return Results.Redirect(direct.ToString());

                var stream = await store.OpenAsync(sha256, ct);

                // enableRangeProcessing viabiliza o resume: o launcher retoma um
                // download interrompido em vez de recomeçar 400 MB.
                //
                // O ETag é o próprio hash, e a imutabilidade é real — o conteúdo
                // de um blob nunca muda, por definição. Isso deixa a CDN
                // (Cloudflare, por exemplo) servir tudo a partir do edge depois
                // do primeiro download.
                return Results.File(
                    stream,
                    "application/octet-stream",
                    enableRangeProcessing: true,
                    entityTag: new EntityTagHeaderValue($"\"{sha256}\""));
            })
            .WithName("DownloadBlob")
            .AllowAnonymous()
            // Anônimo por design, então o teto de transferências simultâneas é a
            // única coisa entre o content store e quem quiser saturar o disco.
            .RequireRateLimiting(RateLimitPolicies.BlobPolicy);

        return app;
    }
}
