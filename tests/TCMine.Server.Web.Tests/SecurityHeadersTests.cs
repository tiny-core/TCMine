using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TCMine.Server.Web.Tests.Infrastructure;

namespace TCMine.Server.Web.Tests;

/// <summary>
///     Cabeçalhos de segurança e política de cookie.
///     Tudo aqui é do tipo que ninguém percebe quando some: a página continua
///     abrindo sem CSP, o login continua funcionando com o cookie sem Secure. O
///     custo só aparece no dia do incidente, e aí não dá para voltar no tempo.
///     As rotas escolhidas não tocam no banco de propósito — o assunto é o
///     pipeline, e arrastar schema para cá só tornaria o teste frágil.
/// </summary>
public class SecurityHeadersTests
{
    private const string RotaSemBanco = "/api/handshake";

    [Theory]
    [InlineData("X-Content-Type-Options", "nosniff")]
    [InlineData("X-Frame-Options", "DENY")]
    [InlineData("Referrer-Policy", "strict-origin-when-cross-origin")]
    public async Task Resposta_traz_o_cabecalho(string nome, string valorEsperado)
    {
        using var factory = new TcMineAppFactory();
        using var client = factory.CreateClient();

        var resposta = await client.GetAsync(RotaSemBanco, TestContext.Current.CancellationToken);

        resposta.Headers.GetValues(nome).ShouldContain(valorEsperado);
    }

    [Theory]
    [InlineData("default-src 'self'")]
    [InlineData("script-src 'self'")]
    [InlineData("frame-ancestors 'none'")]
    [InlineData("base-uri 'self'")]
    [InlineData("form-action 'self'")]
    [InlineData("object-src 'none'")]
    public async Task Csp_declara_a_diretiva(string diretiva)
    {
        var csp = await LerCspAsync();

        csp.ShouldContain(diretiva);
    }

    [Fact]
    public async Task Csp_nao_libera_unsafe_eval()
    {
        var csp = await LerCspAsync();

        // Verificado no navegador: o Monaco carrega e cria editor com workers sob
        // script-src 'self' puro. Se um dia o editor quebrar, a tentação será
        // colar 'unsafe-eval' aqui — que é abrir mão da principal defesa da CSP
        // contra XSS. Este teste força a conversa a acontecer.
        csp.ShouldNotContain("unsafe-eval");
    }

    [Fact]
    public async Task Csp_permite_imagem_externa()
    {
        var csp = await LerCspAsync();

        // Ícone de mod vem do CDN do Modrinth/CurseForge. Apertar isto para
        // 'self' encheria a busca de mods de ícone quebrado.
        csp.ShouldContain("img-src 'self' data: https:");
    }

    [Fact]
    public async Task Hsts_fica_fora_em_desenvolvimento()
    {
        using var factory = new TcMineAppFactory();
        using var client = factory.CreateClient();

        var resposta = await client.SendAsync(
            RequisicaoAtrasDeProxy("painel.exemplo.com"), TestContext.Current.CancellationToken);

        // Em desenvolvimento a app roda em http; prometer https ao navegador
        // trancaria o próprio ambiente local por 30 dias.
        resposta.Headers.Contains("Strict-Transport-Security").ShouldBeFalse();
    }

    [Fact]
    public async Task Hsts_entra_em_producao_atras_do_proxy()
    {
        using var factory = new TcMineAppFactory("Production");
        using var client = factory.CreateClient();

        var resposta = await client.SendAsync(
            RequisicaoAtrasDeProxy("painel.exemplo.com"), TestContext.Current.CancellationToken);

        // Só aparece porque o UseHsts roda DEPOIS do UseForwardedHeaders: é o
        // X-Forwarded-Proto que conta a verdade sobre o esquema atrás do proxy.
        resposta.Headers.GetValues("Strict-Transport-Security")
            .ShouldContain(v => v.Contains("max-age=", StringComparison.Ordinal));
    }

    [Fact]
    public void Cookie_de_sessao_exige_Secure_em_producao()
    {
        using var factory = new TcMineAppFactory("Production");

        var opcoes = factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        opcoes.Cookie.SecurePolicy.ShouldBe(CookieSecurePolicy.Always);
    }

    [Fact]
    public void Cookie_de_antiforgery_exige_Secure_em_producao()
    {
        using var factory = new TcMineAppFactory("Production");

        // O padrão do ASP.NET aqui é SecurePolicy.None — o cookie sai sem Secure
        // mesmo sobre https. Passou despercebido até alguém olhar o Set-Cookie.
        var opcoes = factory.Services.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;

        opcoes.Cookie.SecurePolicy.ShouldBe(CookieSecurePolicy.Always);
    }

    [Fact]
    public void Cookies_aceitam_http_em_desenvolvimento()
    {
        using var factory = new TcMineAppFactory();

        var sessao = factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        // O outro lado da regra: exigir Secure em Development tornaria o login
        // impossível de testar localmente, onde a app roda em http puro.
        sessao.Cookie.SecurePolicy.ShouldBe(CookieSecurePolicy.SameAsRequest);
    }

    private static async Task<string> LerCspAsync()
    {
        using var factory = new TcMineAppFactory();
        using var client = factory.CreateClient();

        var resposta = await client.GetAsync(RotaSemBanco, TestContext.Current.CancellationToken);

        // O framework acrescenta um segundo Content-Security-Policy só com
        // frame-ancestors; juntar os valores evita depender da ordem entre eles.
        return string.Join(" ", resposta.Headers.GetValues("Content-Security-Policy"));
    }

    private static HttpRequestMessage RequisicaoAtrasDeProxy(string host)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Get, RotaSemBanco);

        // O HstsMiddleware ignora localhost por padrão (ExcludedHosts), então um
        // teste apontando para localhost passaria mesmo com o UseHsts removido.
        requisicao.Headers.Host = host;
        requisicao.Headers.Add("X-Forwarded-Proto", "https");
        requisicao.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return requisicao;
    }
}
