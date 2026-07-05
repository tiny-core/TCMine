namespace TCMine_Server.Security;

/// <summary>
///     Cabeçalhos de segurança aplicados a <b>todas</b> as respostas: CSP (defesa em profundidade contra
///     XSS/injeção), anti-clickjacking e afins. A CSP é calibrada para o stack real do painel — Blazor
///     Server + MudBlazor + Monaco (overrides) — que impõe alguns afrouxamentos inevitáveis:
///     - <c>style-src 'unsafe-inline'</c>: o <c>&lt;style&gt;</c> dos designs tokens no <c>App.razor</c> e os
///     <c>style=""</c> inline que o MudBlazor gera (posicionamento de popovers, etc.) não têm nonce viável.
///     - <c>script-src 'self'</c> + <c>'wasm-unsafe-eval'</c>/<c>blob:</c>: os scripts do aplicativo são externos
///     (<c>_framework</c>, MudBlazor, BlazorMonaco); o Monaco cria web workers via <c>blob:</c>.
///     - <c>img-src https:</c>: as thumbnails de mods vêm do CDN do CurseForge (domínios variados).
///     - <c>connect-src 'self'</c>: o WebSocket do Blazor e o SSE <c>/events</c> são mesma-origem.
///     Aplicado cedo no pipeline (antes dos arquivos estáticos) para cobrir também esses. É inofensivo
///     nas respostas de API consumidas pelo launcher (a CSP só age no browser).
/// </summary>
public static class SecurityHeaders
{
    // Política montada uma vez (string imutável). Sem quebras de linha — o header é uma linha só.
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "base-uri 'self'; " +
        "object-src 'none'; " +
        "frame-ancestors 'self'; " +
        "img-src 'self' data: https:; " +
        "font-src 'self' data:; " +
        "style-src 'self' 'unsafe-inline'; " +
        "script-src 'self' 'wasm-unsafe-eval' blob:; " +
        "worker-src 'self' blob:; " +
        "connect-src 'self'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use((ctx, next) =>
        {
            // OnStarting corre logo ANTES do flush dos headers — depois do endpoint. Assim o indexer
            // (que substitui) garante um ÚNICO header CSP mesmo que o framework Blazor acrescente o
            // seu próprio (frame-ancestors 'self', anti-clickjacking do circuito) durante o render.
            ctx.Response.OnStarting(() =>
            {
                var headers = ctx.Response.Headers;
                headers.ContentSecurityPolicy = ContentSecurityPolicy; // substitui (dedup)
                headers.TryAdd("X-Content-Type-Options", "nosniff"); // sem MIME sniffing
                headers.TryAdd("X-Frame-Options", "SAMEORIGIN"); // legado; frame-ancestors cobre o moderno
                headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
                // Desliga APIs de browser que o painel não usa (defesa de superfície)
                headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=(), interest-cohort=()");
                return Task.CompletedTask;
            });

            return next();
        });
    }
}