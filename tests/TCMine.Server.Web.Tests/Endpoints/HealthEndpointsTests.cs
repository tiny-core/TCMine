using System.Net;
using TCMine.Server.Web.Tests.Infrastructure;

namespace TCMine.Server.Web.Tests.Endpoints;

/// <summary>
///     Health check contra a aplicação real.
///     Regressão de um bug de omissão: <c>AddHealthChecks()</c> sem nenhum check
///     registrado responde 200 sempre, inclusive com o banco fora. Um orquestrador
///     apontado para /health mantinha no ar um painel incapaz de abrir uma única
///     página. Estes testes fixam quem depende do banco e quem não depende.
/// </summary>
public class HealthEndpointsTests
{
    /// <summary>
    ///     Configuração que aponta para um Postgres que não existe.
    ///     Production de propósito: em Development o arranque aplica migrations, e
    ///     com o banco inacessível a aplicação nem chega a servir — o que testaria
    ///     outra coisa.
    /// </summary>
    private static TcMineAppFactory ComBancoFora() => new(
        "Production",
        ("Database:Provider", "Postgres"),
        ("Database:ConnectionString",
            "Host=127.0.0.1;Port=1;Database=x;Username=y;Password=z;Timeout=2;Command Timeout=2"));

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Com_banco_de_pe_todas_as_rotas_respondem_saudavel(string rota)
    {
        using var factory = new TcMineAppFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(rota, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    public async Task Com_banco_fora_a_rota_reprova(string rota)
    {
        using var factory = ComBancoFora();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(rota, TestContext.Current.CancellationToken);

        // 503 e não 200: é a diferença entre o orquestrador tirar o painel do
        // balanceador e deixá-lo recebendo tráfego que não consegue atender.
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Liveness_ignora_o_banco()
    {
        using var factory = ComBancoFora();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        // O processo está vivo. Reiniciar o container não levanta um Postgres
        // caído — só derruba o painel junto e apaga as filas, que vivem em
        // memória. Por isso liveness não pode depender do banco.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
