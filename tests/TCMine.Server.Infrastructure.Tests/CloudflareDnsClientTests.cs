using System.Net;
using System.Text;
using System.Text.Json;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Infrastructure.Dns;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     O que vai e o que volta da API da Cloudflare. O formato dos corpos foi
///     conferido contra o SDK oficial; estes testes prendem-no, porque um campo
///     com o nome errado é aceito em silêncio e o registro nasce torto.
/// </summary>
public sealed class CloudflareDnsClientTests
{
    private static readonly DnsZone Zone = new("0123456789abcdef0123456789abcdef", "segredo-do-token");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Srv_vai_com_porta_e_alvo_em_data()
    {
        var handler = new Recorder(HttpStatusCode.OK, """{"success":true,"errors":[],"result":{"id":"r1"}}""");

        await Client(handler).CreateAsync(
            Zone,
            new DnsRecordSpec(
                DnsRecordTypes.Srv, "_minecraft._tcp.a.exemplo.com", "mc.exemplo.com", 25570, "tcmine:abc:srv:1"),
            Ct);

        handler.Method.ShouldBe(HttpMethod.Post);
        handler.Path.ShouldBe("/client/v4/zones/0123456789abcdef0123456789abcdef/dns_records");
        handler.Authorization.ShouldBe("Bearer segredo-do-token");

        var body = JsonDocument.Parse(handler.Body!).RootElement;
        body.GetProperty("type").GetString().ShouldBe("SRV");
        body.GetProperty("name").GetString().ShouldBe("_minecraft._tcp.a.exemplo.com");
        body.GetProperty("comment").GetString().ShouldBe("tcmine:abc:srv:1");
        body.GetProperty("data").GetProperty("port").GetInt32().ShouldBe(25570);
        body.GetProperty("data").GetProperty("target").GetString().ShouldBe("mc.exemplo.com");
    }

    [Fact]
    public async Task Registro_a_vai_sem_proxy()
    {
        // Com a nuvem laranja o nome apontaria para um IP da Cloudflare, que só
        // passa HTTP: o jogo não conectaria.
        var handler = new Recorder(HttpStatusCode.OK, """{"success":true,"errors":[],"result":{"id":"r1"}}""");

        await Client(handler).CreateAsync(
            Zone, new DnsRecordSpec(DnsRecordTypes.A, "mc.exemplo.com", "1.2.3.4", 0, "tcmine:abc:host"), Ct);

        var body = JsonDocument.Parse(handler.Body!).RootElement;
        body.GetProperty("type").GetString().ShouldBe("A");
        body.GetProperty("content").GetString().ShouldBe("1.2.3.4");
        body.GetProperty("proxied").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Listagem_le_a_e_srv()
    {
        var handler = new Recorder(HttpStatusCode.OK,
            """
            {"success":true,"errors":[],"result":[
              {"id":"r1","type":"A","name":"mc.exemplo.com","content":"1.2.3.4","comment":"tcmine:abc:host"},
              {"id":"r2","type":"SRV","name":"_minecraft._tcp.a.exemplo.com","content":"0 0 25570 mc.exemplo.com",
               "comment":"tcmine:abc:srv:1","data":{"priority":0,"weight":0,"port":25570,"target":"mc.exemplo.com"}}
            ]}
            """);

        var records = await Client(handler).ListByCommentPrefixAsync(Zone, "tcmine:abc:", Ct);

        handler.Query!.ShouldContain("comment.startswith=tcmine%3Aabc%3A");

        records.Count.ShouldBe(2);
        records[0].ShouldBe(new DnsRecord("r1", new DnsRecordSpec("A", "mc.exemplo.com", "1.2.3.4", 0, "tcmine:abc:host")));
        records[1].Spec.Target.ShouldBe("mc.exemplo.com");
        records[1].Spec.Port.ShouldBe(25570);
    }

    [Fact]
    public async Task Busca_por_nome_filtra_de_novo_do_nosso_lado()
    {
        // Se a API ignorar o filtro, devolve a zona inteira: a conferência de
        // conflito não pode tomar um registro qualquer por "mesmo nome".
        var handler = new Recorder(HttpStatusCode.OK,
            """
            {"success":true,"errors":[],"result":[
              {"id":"r1","type":"A","name":"MC.exemplo.com","content":"1.2.3.4"},
              {"id":"r2","type":"A","name":"www.exemplo.com","content":"9.9.9.9"}
            ]}
            """);

        var records = await Client(handler).ListByNameAsync(Zone, "mc.exemplo.com", Ct);

        records.Single().Id.ShouldBe("r1");
    }

    [Fact]
    public async Task Token_recusado_vira_mensagem_com_a_dica_e_o_texto_da_cloudflare()
    {
        var handler = new Recorder(HttpStatusCode.Forbidden,
            """{"success":false,"errors":[{"code":10000,"message":"Authentication error"}],"result":null}""");

        var ex = await Should.ThrowAsync<DnsProviderException>(
            () => Client(handler).ListByCommentPrefixAsync(Zone, "tcmine:abc:", Ct));

        ex.Message.ShouldContain("DNS");
        ex.Message.ShouldContain("Authentication error");

        // O token nunca aparece numa mensagem que vai para a tela e para o log.
        ex.Message.ShouldNotContain("segredo-do-token");
    }

    [Fact]
    public async Task Resposta_que_nao_e_json_nao_vira_excecao_de_parse()
    {
        // text/html, como as páginas de erro de verdade: com esse tipo o leitor
        // de JSON nem tenta, e lança outra exceção que não a de parse.
        var handler = new Recorder(HttpStatusCode.BadGateway, "<html>502</html>") { MediaType = "text/html" };

        var ex = await Should.ThrowAsync<DnsProviderException>(
            () => Client(handler).DeleteAsync(Zone, "r1", Ct));

        ex.Message.ShouldContain("502");
    }

    [Fact]
    public async Task Sem_rede_vira_DnsProviderException()
    {
        var handler = new Recorder(HttpStatusCode.OK, "{}") { Throws = true };

        await Should.ThrowAsync<DnsProviderException>(
            () => Client(handler).ListByCommentPrefixAsync(Zone, "tcmine:abc:", Ct));
    }

    private static CloudflareDnsClient Client(Recorder handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.cloudflare.com/client/v4/") });

    private sealed class Recorder(HttpStatusCode status, string response) : HttpMessageHandler
    {
        public bool Throws { get; init; }
        public string MediaType { get; init; } = "application/json";
        public HttpMethod? Method { get; private set; }
        public string? Path { get; private set; }
        public string? Query { get; private set; }
        public string? Authorization { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (Throws)
                throw new HttpRequestException("sem rede");

            Method = request.Method;
            Path = request.RequestUri!.AbsolutePath;
            Query = request.RequestUri.Query;
            Authorization = request.Headers.Authorization?.ToString();
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(response, Encoding.UTF8, MediaType)
            };
        }
    }
}
