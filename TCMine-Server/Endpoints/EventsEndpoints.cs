using TCMine_Services.Server;

namespace TCMine_Server.Endpoints;

/// <summary>
/// Stream de eventos (Server-Sent Events) consumido pelo <b>launcher</b>: avisa que o conteúdo
/// público mudou. O launcher liga-se a <c>/events</c>, fixa a versão inicial como baseline e
/// recarrega o catálogo de modpacks sempre que recebe uma versão maior.
///
/// Leitura pública (sem auth), tal como <c>/api/modpacks</c> — não há segredos no fluxo, só um
/// contador de versão. O <see cref="ContentNotifier"/> é a fonte das notificações.
/// </summary>
public static class EventsEndpoints
{
    public static void MapEventsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/events", async (HttpContext ctx, ContentNotifier notifier, CancellationToken ct) =>
        {
            ctx.Response.Headers.ContentType = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers["X-Accel-Buffering"] = "no"; // desliga o buffering do nginx

            var reader = notifier.Subscribe(out var channel);
            try
            {
                // Evento inicial com a versão atual — o cliente fixa-a como baseline.
                await ctx.Response.WriteAsync($"data: {notifier.Version}\n\n", ct);
                await ctx.Response.Body.FlushAsync(ct);

                while (!ct.IsCancellationRequested)
                {
                    long version;
                    try
                    {
                        // Espera por uma nova versão, mas com keep-alive a cada 25s: um comentário
                        // SSE (": …") mantém a ligação viva mediante proxies/firewalls que cortam
                        // conexões ociosas, sem ser interpretado como dado pelo cliente.
                        using var hb = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        hb.CancelAfter(TimeSpan.FromSeconds(25));
                        version = await reader.ReadAsync(hb.Token);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        await ctx.Response.WriteAsync(": keep-alive\n\n", ct);
                        await ctx.Response.Body.FlushAsync(ct);
                        continue;
                    }

                    await ctx.Response.WriteAsync($"data: {version}\n\n", ct);
                    await ctx.Response.Body.FlushAsync(ct);
                }
            }
            catch (OperationCanceledException)
            {
                // Cliente desligou — comportamento normal, não é erro.
            }
            finally
            {
                notifier.Unsubscribe(channel);
            }
        });
    }
}