using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TCMine.Server.Infrastructure.Updates;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     O repositório publica servidor E launcher, rascunhos e betas: o aviso só
///     pode olhar para a estável mais nova do SERVIDOR.
/// </summary>
public sealed class GitHubServerReleaseFeedTests
{
    private const string Releases = """
                                    [
                                      {"tag_name":"launcher-v9.0.0","html_url":"https://github.com/t/r/releases/l9","draft":false,"prerelease":false},
                                      {"tag_name":"server-v0.6.0-beta.1","html_url":"https://github.com/t/r/releases/b","draft":false,"prerelease":true},
                                      {"tag_name":"server-v0.7.0","html_url":"https://github.com/t/r/releases/d","draft":true,"prerelease":false},
                                      {"tag_name":"server-v0.5.0","html_url":"https://github.com/t/r/releases/5","draft":false,"prerelease":false},
                                      {"tag_name":"server-v0.10.0","html_url":"https://github.com/t/r/releases/10","draft":false,"prerelease":false},
                                      {"tag_name":"server-v0.9.0","html_url":"https://github.com/t/r/releases/9","draft":false,"prerelease":false}
                                    ]
                                    """;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Escolhe_a_estavel_mais_nova_do_servidor()
    {
        var handler = new Respostas(HttpStatusCode.OK, Releases);
        var feed = Feed(handler);

        var latest = await feed.GetLatestStableAsync(Ct);

        latest.ShouldNotBeNull();
        latest.Version.ShouldBe("0.10.0");
        latest.Page.ToString().ShouldBe("https://github.com/t/r/releases/10");
    }

    [Fact]
    public async Task Guarda_em_cache_e_nao_consulta_de_novo()
    {
        var handler = new Respostas(HttpStatusCode.OK, Releases);
        var feed = Feed(handler);

        await feed.GetLatestStableAsync(Ct);
        await feed.GetLatestStableAsync(Ct);

        handler.Chamadas.ShouldBe(1);
    }

    [Fact]
    public async Task GitHub_fora_do_ar_devolve_nulo_sem_lancar()
    {
        var feed = Feed(new Respostas(HttpStatusCode.ServiceUnavailable, "{}"));

        (await feed.GetLatestStableAsync(Ct)).ShouldBeNull();
    }

    private static GitHubServerReleaseFeed Feed(Respostas handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com") },
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new UpdateOptions()),
            NullLogger<GitHubServerReleaseFeed>.Instance);

    private sealed class Respostas(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Chamadas { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Chamadas++;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
