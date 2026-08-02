using Microsoft.Net.Http.Headers;
using TCMine.Server.Application.Abstractions;

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
                if (!await store.ExistsAsync(sha256, ct))
                    return Results.NotFound();

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
            .AllowAnonymous();

        return app;
    }
}
