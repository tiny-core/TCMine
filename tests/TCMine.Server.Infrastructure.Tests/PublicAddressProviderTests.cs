using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using TCMine.Server.Infrastructure.Network;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     O IP público vem de serviços de terceiros, sem contrato: o que se testa é
///     que nenhuma resposta deles — fora do ar, lenta, ou uma página que não é um
///     IP — vira exceção ou endereço de servidor.
/// </summary>
public sealed class PublicAddressProviderTests
{
    private const string Trace = "fl=123abc\nh=www.cloudflare.com\nip=94.63.98.255\nts=1760000000.000\nloc=PT\n";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Le_o_ip_do_trace_da_cloudflare()
    {
        var handler = new Sources { Cloudflare = (HttpStatusCode.OK, Trace) };

        (await Provider(handler).GetAsync(Ct)).ShouldBe("94.63.98.255");
        handler.Calls.ShouldBe(1, "com a primeira fonte respondendo, a segunda não é consultada");
    }

    [Fact]
    public async Task Cloudflare_fora_do_ar_cai_no_ipify()
    {
        var handler = new Sources
        {
            Cloudflare = (HttpStatusCode.ServiceUnavailable, ""),
            Ipify = (HttpStatusCode.OK, "94.63.98.255\n")
        };

        (await Provider(handler).GetAsync(Ct)).ShouldBe("94.63.98.255");
    }

    [Fact]
    public async Task Resposta_que_nao_e_ip_nao_vira_endereco()
    {
        // O portal de um Wi-Fi, ou uma página de erro com status 200.
        var handler = new Sources
        {
            Cloudflare = (HttpStatusCode.OK, "<html>entre na rede</html>"),
            Ipify = (HttpStatusCode.OK, "<html>entre na rede</html>")
        };

        (await Provider(handler).GetAsync(Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Sem_rede_devolve_nulo_sem_lancar()
    {
        var handler = new Sources { Throws = true };

        (await Provider(handler).GetAsync(Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Guarda_em_cache_o_acerto_e_o_erro()
    {
        // O erro também: sem isso, uma rede fora do ar custaria o tempo limite
        // das duas fontes a cada tela aberta.
        var ok = new Sources { Cloudflare = (HttpStatusCode.OK, Trace) };
        var okProvider = Provider(ok);
        await okProvider.GetAsync(Ct);
        await okProvider.GetAsync(Ct);
        ok.Calls.ShouldBe(1);

        var down = new Sources { Throws = true };
        var downProvider = Provider(down);
        await downProvider.GetAsync(Ct);
        await downProvider.GetAsync(Ct);
        down.Calls.ShouldBe(2, "uma tentativa por fonte, e só na primeira consulta");
    }

    [Theory]
    [InlineData("ip=2001:db8::1\n", "2001:db8::1")]
    [InlineData("h=x\r\nip=1.2.3.4\r\n", "1.2.3.4")] // fim de linha do Windows
    [InlineData("h=x\nloc=PT\n", null)] // sem a chave
    [InlineData("ip=nao-e-ip\n", null)]
    public void Trace_so_devolve_um_ip_de_verdade(string body, string? expected)
    {
        PublicAddressProvider.ParseTrace(body).ShouldBe(expected);
    }

    private static PublicAddressProvider Provider(Sources handler) =>
        new(new HttpClient(handler),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<PublicAddressProvider>.Instance);

    /// <summary>Responde por host: cada fonte com o seu status e corpo.</summary>
    private sealed class Sources : HttpMessageHandler
    {
        public (HttpStatusCode Status, string Body) Cloudflare { get; init; } = (HttpStatusCode.NotFound, "");
        public (HttpStatusCode Status, string Body) Ipify { get; init; } = (HttpStatusCode.NotFound, "");
        public bool Throws { get; init; }
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;

            if (Throws)
                throw new HttpRequestException("sem rede");

            var (status, body) = request.RequestUri!.Host.Contains("cloudflare", StringComparison.Ordinal)
                ? Cloudflare
                : Ipify;

            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }
}
