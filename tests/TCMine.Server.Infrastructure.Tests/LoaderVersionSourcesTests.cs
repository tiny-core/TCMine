using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Infrastructure.Versions;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     A lista de builds que o painel oferece tem de ser a que serve ÀQUELE
///     Minecraft. Antes, Fabric e Quilt devolviam todo loader que já existiu, e
///     o NeoForge devolvia tudo quando não reconhecia a versão do jogo — o
///     modpack era publicado com uma combinação que o container não tem como
///     instalar. Aqui ficam travados o filtro de cada fonte e a diferença entre
///     "a fonte respondeu que não há" e "a fonte não respondeu".
/// </summary>
public sealed class LoaderVersionSourcesTests
{
    // maven-metadata.xml do artefato novo (net.neoforged:neoforge), do mais
    // antigo para o mais novo, como o maven publica.
    private const string NeoForge = """
                                    <metadata><versioning><versions>
                                      <version>20.4.237</version>
                                      <version>21.0.167</version>
                                      <version>21.1.1</version>
                                      <version>21.1.200-beta</version>
                                      <version>21.1.209</version>
                                      <version>21.10.5</version>
                                      <version>26.1.0.3-beta</version>
                                      <version>26.1.0.7</version>
                                      <version>26.1.2.1</version>
                                    </versions></versioning></metadata>
                                    """;

    // O artefato antigo (net.neoforged:forge), com a numeração herdada do Forge.
    private const string NeoForgeLegacy = """
                                          <metadata><versioning><versions>
                                            <version>1.20.1-47.1.5</version>
                                            <version>1.20.1-47.1.106</version>
                                          </versions></versioning></metadata>
                                          """;

    private const string Fabric = """
                                  [
                                    {"loader":{"version":"0.17.0-beta.1","stable":false},"intermediary":{}},
                                    {"loader":{"version":"0.16.14","stable":true},"intermediary":{}}
                                  ]
                                  """;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("1.21.1", "21.1.")]
    [InlineData("1.21", "21.0.")]
    [InlineData("1.20.4", "20.4.")]
    [InlineData("26.1", "26.1.0.")]
    [InlineData("26.1.2", "26.1.2.")]
    public void NeoForge_codifica_o_Minecraft_no_comeco_da_versao(string minecraft, string prefix) =>
        NeoForgeVersionSource.ToNeoPrefix(minecraft).ShouldBe(prefix);

    [Theory]
    [InlineData("24w14a")]
    [InlineData("1.21.1-pre1")]
    [InlineData("1")]
    [InlineData("")]
    public void NeoForge_nao_inventa_prefixo_para_esquema_desconhecido(string minecraft) =>
        NeoForgeVersionSource.ToNeoPrefix(minecraft).ShouldBeNull();

    [Fact]
    public async Task NeoForge_devolve_so_as_builds_daquele_Minecraft_da_mais_nova_para_a_mais_antiga()
    {
        var source = new NeoForgeVersionSource(new HttpClient(new Resposta(NeoForge)));

        (await source.GetAsync("1.21.1", false, Ct)).ShouldBe(["21.1.209", "21.1.200-beta", "21.1.1"]);

        // "21.1." não pode casar com "21.10.5": é outro Minecraft (1.21.10).
        (await source.GetAsync("1.21.10", false, Ct)).ShouldBe(["21.10.5"]);
    }

    [Fact]
    public async Task NeoForge_so_releases_tira_as_de_sufixo()
    {
        var source = new NeoForgeVersionSource(new HttpClient(new Resposta(NeoForge)));

        (await source.GetAsync("1.21.1", true, Ct)).ShouldBe(["21.1.209", "21.1.1"]);
        (await source.GetAsync("26.1", true, Ct)).ShouldBe(["26.1.0.7"]);
    }

    [Fact]
    public async Task NeoForge_do_1_20_1_vem_do_artefato_antigo_sem_o_prefixo_do_jogo()
    {
        var handler = new Resposta(NeoForgeLegacy);

        var versions = await new NeoForgeVersionSource(new HttpClient(handler)).GetAsync("1.20.1", false, Ct);

        versions.ShouldBe(["47.1.106", "47.1.5"]);
        handler.Pedido.ShouldNotBeNull();
        handler.Pedido.AbsolutePath.ShouldContain("/net/neoforged/forge/");
    }

    [Fact]
    public async Task NeoForge_de_esquema_desconhecido_e_lista_vazia_e_nao_tudo()
    {
        var handler = new Resposta(NeoForge);

        var versions = await new NeoForgeVersionSource(new HttpClient(handler)).GetAsync("24w14a", false, Ct);

        versions.ShouldBeEmpty();
        // Nem consulta: não há o que filtrar.
        handler.Pedido.ShouldBeNull();
    }

    [Fact]
    public async Task Fabric_consulta_por_versao_do_jogo_e_respeita_o_campo_stable()
    {
        var handler = new Resposta(Fabric);
        var source = new FabricVersionSource(new HttpClient(handler));

        (await source.GetAsync("1.21.1", false, Ct)).ShouldBe(["0.17.0-beta.1", "0.16.14"]);
        (await source.GetAsync("1.21.1", true, Ct)).ShouldBe(["0.16.14"]);

        handler.Pedido.ShouldNotBeNull();
        handler.Pedido.Host.ShouldBe("meta.fabricmc.net");
        handler.Pedido.AbsolutePath.ShouldEndWith("/versions/loader/1.21.1");
    }

    [Fact]
    public async Task Fonte_fora_do_ar_e_falha_e_nao_fica_em_cache()
    {
        // A falha em cache fazia uma queda de segundos virar meia hora de
        // "não há versões" — e a validação recusaria um modpack bom.
        var handler = new Resposta(Fabric) { Status = HttpStatusCode.ServiceUnavailable };
        var catalog = Catalog(handler);

        var falha = await catalog.GetLoaderVersionsAsync(ModLoader.Fabric, "1.21.1", false, Ct);

        falha.Failed.ShouldBeTrue();
        falha.Versions.ShouldBeEmpty();

        handler.Status = HttpStatusCode.OK;

        var acerto = await catalog.GetLoaderVersionsAsync(ModLoader.Fabric, "1.21.1", false, Ct);

        acerto.Failed.ShouldBeFalse();
        acerto.Versions.ShouldBe(["0.17.0-beta.1", "0.16.14"]);
    }

    [Fact]
    public async Task Lista_vazia_que_a_fonte_deu_e_resposta_e_fica_em_cache()
    {
        var handler = new Resposta("[]");
        var catalog = Catalog(handler);

        var primeira = await catalog.GetLoaderVersionsAsync(ModLoader.Fabric, "1.2.5", false, Ct);
        await catalog.GetLoaderVersionsAsync(ModLoader.Fabric, "1.2.5", false, Ct);

        primeira.Failed.ShouldBeFalse();
        primeira.Versions.ShouldBeEmpty();
        handler.Pedidos.ShouldBe(1);
    }

    private static VersionCatalog Catalog(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler);

        return new VersionCatalog(
            new MinecraftVersionSource(http),
            new NeoForgeVersionSource(http),
            new ForgeVersionSource(http),
            new FabricVersionSource(http),
            new QuiltVersionSource(http),
            new MemoryCache(new MemoryCacheOptions()));
    }

    private sealed class Resposta(string body) : HttpMessageHandler
    {
        public Uri? Pedido { get; private set; }

        public int Pedidos { get; private set; }

        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Pedido = request.RequestUri;
            Pedidos++;

            return Task.FromResult(new HttpResponseMessage(Status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
