using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using TCMine.Server.Web.Tests.Infrastructure;

namespace TCMine.Server.Web.Tests.Endpoints;

/// <summary>
///     Limite de taxa nas rotas de autenticação.
///     Sem ele, /auth/login aceita tentativas infinitas: além da força bruta, o
///     hash de senha é caro por design, e algumas centenas de POSTs simultâneos
///     saturam CPU e derrubam o painel — o mesmo endpoint serve de porta e de
///     alavanca de negação de serviço.
///     Cada teste monta a própria aplicação porque o contador vive no host: dois
///     testes no mesmo host disputariam a mesma cota e a ordem de execução
///     decidiria quem passa.
/// </summary>
public class AuthRateLimitTests
{
    /// <summary>Igual ao PermitLimit da política; a 11ª é a que tem de bater na trave.</summary>
    private const int Permitidas = 10;

    [Fact]
    public async Task Decima_primeira_tentativa_de_login_e_bloqueada()
    {
        using var factory = new TcMineAppFactory();
        using var client = CriarCliente(factory);

        for (var i = 1; i <= Permitidas; i++)
        {
            var permitida = await TentarLoginAsync(client);

            // As dez primeiras chegam ao endpoint. Falham por credencial ou por
            // antiforgery — o que importa é que passaram pelo limitador.
            EhBloqueio(permitida).ShouldBeFalse($"a tentativa {i} não deveria ter sido bloqueada");
        }

        var excedente = await TentarLoginAsync(client);

        EhBloqueio(excedente).ShouldBeTrue("a 11ª tentativa deveria ter sido bloqueada");
    }

    [Fact]
    public async Task Bloqueio_devolve_o_admin_para_a_tela_com_a_mensagem()
    {
        using var factory = new TcMineAppFactory();
        using var client = CriarCliente(factory);

        for (var i = 0; i <= Permitidas; i++)
            await TentarLoginAsync(client);

        var resposta = await TentarLoginAsync(client);

        // Post de formulário vem do navegador: 429 cru seria uma página branca.
        // Volta para /login com ?error=, que a tela já sabe exibir.
        resposta.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        var destino = resposta.Headers.Location!.ToString();
        destino.ShouldStartWith("/login?error=");
        Uri.UnescapeDataString(destino).ShouldContain("Tentativas demais");
    }

    [Fact]
    public async Task Health_nao_entra_na_cota_de_autenticacao()
    {
        using var factory = new TcMineAppFactory();
        using var client = CriarCliente(factory);

        for (var i = 0; i <= Permitidas; i++)
            await TentarLoginAsync(client);

        var health = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        // A política é por endpoint, nunca global: limitar o painel inteiro
        // derrubaria o circuito do Blazor de quem está usando.
        health.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static HttpClient CriarCliente(TcMineAppFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static Task<HttpResponseMessage> TentarLoginAsync(HttpClient client) =>
        client.PostAsync(
            "/auth/login",
            new FormUrlEncodedContent([
                new KeyValuePair<string, string>("email", "ninguem@teste.local"),
                new KeyValuePair<string, string>("password", "errada")
            ]),
            TestContext.Current.CancellationToken);

    /// <summary>
    ///     Distingue o redirecionamento do limitador do redirecionamento normal de
    ///     credencial inválida — os dois são 302, e olhar só o status faria o teste
    ///     passar sem limitador nenhum.
    /// </summary>
    private static bool EhBloqueio(HttpResponseMessage resposta) =>
        resposta.StatusCode == HttpStatusCode.Redirect
        && Uri.UnescapeDataString(resposta.Headers.Location?.ToString() ?? "")
            .Contains("Tentativas demais", StringComparison.Ordinal);
}
