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
                HttpContext http,
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
                {
                    // Este 302 NÃO pode ser guardado: a assinatura do destino
                    // expira em minutos, e um cache que o reaproveitasse depois
                    // mandaria o launcher a uma URL morta. Sem o cabeçalho, um
                    // cache intermediário ainda poderia guardá-lo por heurística.
                    http.Response.Headers.CacheControl = "no-store";
                    return Results.Redirect(direct.ToString());
                }

                var stream = await store.OpenAsync(sha256, ct);

                // Content-addressed: a URL contém o hash do conteúdo, então o
                // corpo desta resposta não pode mudar — a imutabilidade não é uma
                // promessa, é a definição. 'immutable' diz ao cliente e à CDN que
                // nem vale revalidar; sem ele, o ETag garante a corretude mas
                // cada download ainda paga uma ida ao origin para ouvir 304.
                http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";

                // enableRangeProcessing viabiliza o resume: o launcher retoma um
                // download interrompido em vez de recomeçar 400 MB.
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
