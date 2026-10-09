namespace TCMine.Server.Web.Configuration;

/// <summary>
///     Cabeçalhos de segurança da resposta.
///     Cada diretiva abaixo tem um motivo e, quando é frouxa, tem a justificativa
///     da frouxidão. Uma CSP copiada de tutorial ou quebra a aplicação, ou é tão
///     permissiva que só serve de enfeite — e nos dois casos alguém a desliga na
///     primeira sexta-feira ruim.
/// </summary>
public static class SecurityHeaders
{
    /// <summary>
    ///     Montada uma vez: é a mesma string em toda resposta, e concatenar isto a
    ///     cada requisição seria alocação pura.
    /// </summary>
    private static readonly string ContentSecurityPolicy = string.Join("; ", "default-src 'self'", "script-src 'self'",
        "style-src 'self' 'unsafe-inline'", "img-src 'self' data: https:", "font-src 'self' data:",
        "connect-src 'self'", "worker-src 'self' blob:", "frame-ancestors 'none'", "base-uri 'self'",
        "form-action 'self'", "object-src 'none'");

    /// <summary>
    ///     Aplica os cabeçalhos a toda resposta.
    ///     Antes do <paramref name="next" />: depois que a resposta começa a ser
    ///     escrita os cabeçalhos já foram enviados, e a atribuição seria ignorada
    ///     em silêncio — justamente nos downloads, que é onde a resposta começa
    ///     mais cedo.
    /// </summary>
    public static IApplicationBuilder UseTcMineSecurityHeaders(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;

            headers.ContentSecurityPolicy = ContentSecurityPolicy;

            // Impede o navegador de "adivinhar" o tipo do conteúdo: sem isto, um
            // blob que devolvemos como octet-stream pode ser interpretado como
            // HTML e executar script na nossa origem.
            headers.XContentTypeOptions = "nosniff";

            headers.XFrameOptions = "DENY";

            // Não vaza o caminho interno do painel para sites externos; mantém só
            // a origem, e nada em navegação insegura.
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            await next();
        });

        return app;
    }
}
