using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using TCMine.Server.Web.Tests.Infrastructure;

namespace TCMine.Server.Web.Tests.Endpoints;

/// <summary>
///     Contrato das rotas: quem exige sessão, quem é anônimo de propósito, e o
///     que cada uma devolve para entrada inválida.
///     O padrão do painel é "precisa de sessão", com as exceções marcadas uma a
///     uma — e é justamente uma exceção esquecida (ou uma a mais) que ninguém
///     nota até virar incidente.
/// </summary>
public class EndpointContractTests : IClassFixture<EndpointContractTests.Fixture>
{
    private readonly TcMineAppFactory _factory;

    public EndpointContractTests(Fixture fixture) => _factory = fixture.Factory;

    private HttpClient Cliente =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Painel_sem_sessao_vai_para_o_login()
    {
        var resposta = await Cliente.GetAsync("/", TestContext.Current.CancellationToken);

        resposta.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        // O middleware de cookie devolve Location absoluto; o nosso OnRejected
        // devolve relativo. Comparar o caminho vale para os dois.
        var destino = resposta.Headers.Location!;
        var caminho = destino.IsAbsoluteUri ? destino.AbsolutePath : destino.ToString();

        caminho.ShouldStartWith("/login");
    }

    [Fact]
    public async Task Backup_de_mundo_sem_sessao_nao_serve_arquivo()
    {
        var rota = $"/api/v1/servers/{Guid.CreateVersion7()}/backups/{Guid.CreateVersion7()}";

        var resposta = await Cliente.GetAsync(rota, TestContext.Current.CancellationToken);

        // Um backup carrega dados dos jogadores. Seja 302 para o login ou 401, o
        // que não pode acontecer é 200.
        resposta.StatusCode.ShouldNotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Handshake_responde_sem_sessao()
    {
        // Anônimo por necessidade: o launcher precisa descobrir se é compatível
        // ANTES de conseguir autenticar. Se um dia isto passar a exigir sessão,
        // todo cliente antigo perde a mensagem de "atualize".
        var resposta = await Cliente.GetAsync("/api/handshake", TestContext.Current.CancellationToken);

        resposta.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Blob_com_hash_malformado_e_erro_do_cliente()
    {
        var resposta = await Cliente.GetAsync("/api/v1/blobs/nao-e-hash", TestContext.Current.CancellationToken);

        // 400 e não 500: o hash vem da URL, então formato inválido é erro de
        // quem pediu.
        resposta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Blob_inexistente_devolve_404()
    {
        var ausente = new string('0', 64);

        var resposta = await Cliente.GetAsync($"/api/v1/blobs/{ausente}", TestContext.Current.CancellationToken);

        resposta.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    ///     Uma aplicação para a classe toda: nenhum destes testes mexe em estado
    ///     do host, e subir sete vezes custaria mais que o resto da suíte junta.
    /// </summary>
    public sealed class Fixture : IDisposable
    {
        // Propriedade internal numa classe pública: o xUnit exige que a fixture
        // seja pública, mas a factory herda de WebApplicationFactory<Program> e o
        // Program gerado pelos top-level statements é internal.
        internal TcMineAppFactory Factory { get; } = new();

        public void Dispose() => Factory.Dispose();
    }
}
