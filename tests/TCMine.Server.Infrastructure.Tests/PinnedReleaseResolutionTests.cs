using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Infrastructure.Ingestion.Modrinth;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     Release fixada (pack importado, versão escolhida pelo admin) é resolvida
///     PELO ID. Antes ela era procurada na lista das compatíveis e, não achada,
///     trocada em silêncio pela mais recente.
/// </summary>
public sealed class PinnedReleaseResolutionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Modrinth_resolve_a_release_fixada_e_nao_a_mais_recente()
    {
        var handler = new Respostas
        {
            ["/v2/version/antiga"] = Versao("antiga", "1.0.0"),
            // A lista das compatíveis só traz a nova: era aqui que a antiga "sumia".
            ["/v2/project/jei/version"] = $"[{Versao("nova", "2.0.0")}]"
        };
        var resolver = new ModrinthModResolver(new HttpClient(handler), NullLogger<ModrinthModResolver>.Instance);

        var result = await resolver.ResolveAsync(new ModRequest("jei", "antiga", "1.21.1", ModLoader.NeoForge), Ct);

        result.ShouldBeOfType<ModResolution.Resolved>().VersionId.ShouldBe("antiga");
    }

    [Fact]
    public async Task Modrinth_sem_fixacao_leva_a_mais_recente_compativel()
    {
        var handler = new Respostas { ["/v2/project/jei/version"] = $"[{Versao("nova", "2.0.0")}]" };
        var resolver = new ModrinthModResolver(new HttpClient(handler), NullLogger<ModrinthModResolver>.Instance);

        var result = await resolver.ResolveAsync(new ModRequest("jei", null, "1.21.1", ModLoader.NeoForge), Ct);

        result.ShouldBeOfType<ModResolution.Resolved>().VersionId.ShouldBe("nova");
    }

    private static string Versao(string id, string numero) =>
        $$"""
          {"id":"{{id}}","version_number":"{{numero}}","game_versions":["1.21.1"],"loaders":["neoforge"],
           "version_type":"release","date_published":"2026-01-01T00:00:00Z",
           "files":[{"url":"https://cdn.modrinth.com/{{id}}.jar","filename":"jei-{{numero}}.jar","size":3,
                     "hashes":{"sha1":"x"},"primary":true}],
           "dependencies":[]}
          """;

    /// <summary>Responde pelo caminho (sem query); o resto é 404.</summary>
    private sealed class Respostas : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _porCaminho = new(StringComparer.Ordinal);

        public string this[string path] { set => _porCaminho[path] = value; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(_porCaminho.TryGetValue(request.RequestUri!.AbsolutePath, out var json)
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
