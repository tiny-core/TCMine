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
    private static readonly string ContentSecurityPolicy = string.Join("; ",
    [
        // Tudo o que não estiver explicitado abaixo só pode vir da própria origem.
        "default-src 'self'",

        // Todo script do painel é arquivo servido por nós — não há <script> inline
        // em nenhum .razor, o que permite manter 'self' puro, sem nonce nem hash.
        "script-src 'self'",

        // 'unsafe-inline' aqui não é descuido: o MudBlazor posiciona popovers e
        // diálogos escrevendo style="" no elemento, e o próprio Blazor injeta o
        // <style> da UI de reconexão. Sem isto, metade dos menus abre no canto
        // errado e o aviso de "reconectando" fica invisível.
        "style-src 'self' 'unsafe-inline'",

        // Ícone de mod vem do CDN de quem hospeda o mod (Modrinth, CurseForge), e
        // quem escolhe o host é a API deles, não nós. Enumerar os domínios
        // quebraria em silêncio no dia em que trocassem de CDN. Imagem é recurso
        // passivo: o risco de aceitar qualquer https é baixo, e o de listar
        // errado é uma tela cheia de ícone quebrado.
        "img-src 'self' data: https:",

        "font-src 'self' data:",

        // Cobre o WebSocket do circuito Blazor e do hub: 'self' inclui ws/wss da
        // mesma origem.
        "connect-src 'self'",

        // O editor de overrides (Monaco) cria seus workers a partir de blob:.
        "worker-src 'self' blob:",

        // Clickjacking: ninguém embute o painel num iframe. frame-ancestors é a
        // versão moderna; o X-Frame-Options abaixo cobre navegador antigo.
        "frame-ancestors 'none'",

        // Fecha o <base> injetado por XSS, que reescreveria todo caminho relativo.
        "base-uri 'self'",

        // Formulário só posta para nós — corta o roubo de credencial por form
        // reescrito para host externo.
        "form-action 'self'",

        "object-src 'none'"
    ]);

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
