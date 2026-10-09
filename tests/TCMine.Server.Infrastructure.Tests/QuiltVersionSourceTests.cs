using System.Net;
using System.Text;
using TCMine.Server.Infrastructure.Versions;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     A fonte do Quilt consultava a meta do FABRIC: o painel listava versões de
///     loader que o Quilt não tem. Estes testes travam o endereço e a regra de
///     "estável", que na meta do Quilt não vem num campo — vem no sufixo da versão.
/// </summary>
public sealed class QuiltVersionSourceTests
{
    // Formato real de https://meta.quiltmc.org/v3/versions/loader (sem "stable").
    private const string Loaders = """
                                   [
                                     {"separator":".","build":4,"maven":"org.quiltmc:quilt-loader:0.31.0-beta.4","version":"0.31.0-beta.4"},
                                     {"separator":".","build":0,"maven":"org.quiltmc:quilt-loader:0.30.0","version":"0.30.0"},
                                     {"separator":".","build":2,"maven":"org.quiltmc:quilt-loader:0.30.0-beta.2","version":"0.30.0-beta.2"},
                                     {"separator":".","build":3,"maven":"org.quiltmc:quilt-loader:0.29.3","version":"0.29.3"}
                                   ]
                                   """;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Consulta_a_meta_do_Quilt_e_nao_a_do_Fabric()
    {
        var handler = new Resposta(Loaders);

        await new QuiltVersionSource(new HttpClient(handler)).GetAsync(false, Ct);

        handler.Pedido.ShouldNotBeNull();
        handler.Pedido.Host.ShouldBe("meta.quiltmc.org");
    }

    [Fact]
    public async Task Sem_filtro_devolve_todas_na_ordem_da_meta()
    {
        var versions = await new QuiltVersionSource(new HttpClient(new Resposta(Loaders))).GetAsync(false, Ct);

        versions.ShouldBe(["0.31.0-beta.4", "0.30.0", "0.30.0-beta.2", "0.29.3"]);
    }

    [Fact]
    public async Task So_estaveis_tira_as_de_sufixo_pre_lancamento()
    {
        // A meta do Quilt não tem "stable". Se o filtro voltar a depender desse
        // campo, esta lista sai vazia.
        var versions = await new QuiltVersionSource(new HttpClient(new Resposta(Loaders))).GetAsync(true, Ct);

        versions.ShouldBe(["0.30.0", "0.29.3"]);
    }

    private sealed class Resposta(string body) : HttpMessageHandler
    {
        public Uri? Pedido { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Pedido = request.RequestUri;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
