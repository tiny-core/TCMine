using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TCMine.Server.Application.Abstractions;
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

        var lookup = await feed.LookupAsync(refresh: false, Ct);

        lookup.State.ShouldBe(ReleaseFeedState.Ok);
        lookup.Latest.ShouldNotBeNull();
        lookup.Latest.Version.ShouldBe("0.10.0");
        lookup.Latest.Page.ToString().ShouldBe("https://github.com/t/r/releases/10");
    }

    [Fact]
    public async Task Guarda_em_cache_e_nao_consulta_de_novo()
    {
        var handler = new Respostas(HttpStatusCode.OK, Releases);
        var feed = Feed(handler);

        await feed.LookupAsync(refresh: false, Ct);
        await feed.LookupAsync(refresh: false, Ct);

        handler.Chamadas.ShouldBe(1);
    }

    [Fact]
    public async Task Verificar_agora_passa_por_cima_do_cache()
    {
        // Quem acabou de publicar não pode ficar preso à resposta de antes.
        var handler = new Respostas(HttpStatusCode.OK, Releases);
        var feed = Feed(handler);

        await feed.LookupAsync(refresh: false, Ct);
        await feed.LookupAsync(refresh: true, Ct);

        handler.Chamadas.ShouldBe(2);
    }

    [Fact]
    public async Task GitHub_fora_do_ar_diz_que_nao_deu_para_saber_sem_lancar()
    {
        // "Sem release" e "não consegui perguntar" pedem frases diferentes.
        var feed = Feed(new Respostas(HttpStatusCode.ServiceUnavailable, "{}"));

        var lookup = await feed.LookupAsync(refresh: false, Ct);

        lookup.State.ShouldBe(ReleaseFeedState.Unreachable);
        lookup.Latest.ShouldBeNull();
    }

    [Fact]
    public async Task Sem_release_estavel_e_resposta_valida_e_nao_falha()
    {
        var feed = Feed(new Respostas(HttpStatusCode.OK, "[]"));

        var lookup = await feed.LookupAsync(refresh: false, Ct);

        lookup.State.ShouldBe(ReleaseFeedState.Ok);
        lookup.Latest.ShouldBeNull();
    }

    [Fact]
    public async Task Desligado_na_configuracao_nao_sai_para_a_internet()
    {
        var handler = new Respostas(HttpStatusCode.OK, Releases);
        var feed = Feed(handler, new UpdateOptions { Enabled = false });

        (await feed.LookupAsync(refresh: true, Ct)).State.ShouldBe(ReleaseFeedState.Disabled);
        handler.Chamadas.ShouldBe(0);
    }

    private static GitHubServerReleaseFeed Feed(Respostas handler, UpdateOptions? options = null) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com") },
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(options ?? new UpdateOptions()),
            TimeProvider.System,
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
