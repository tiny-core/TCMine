using System.Net;
using System.Security.Cryptography;
using System.Text;
using TCMine.Server.Web.Tests.Infrastructure;

namespace TCMine.Server.Web.Tests.Endpoints;

/// <summary>
///     Cabeçalhos de cache do download de blob.
///     Um blob é content-addressed: a URL carrega o hash do conteúdo, então o
///     corpo não pode mudar. O ETag sozinho já garantia a corretude, mas obrigava
///     cada download a uma ida ao origin só para ouvir 304 — com um modpack de
///     centenas de arquivos, isso é centenas de idas por instalação.
/// </summary>
public sealed class BlobCacheTests : IDisposable
{
    private readonly string _raiz = Path.Combine(
        Path.GetTempPath(), $"tcmine-blobs-{Guid.CreateVersion7():N}");

    [Fact]
    public async Task Blob_existente_pode_ser_guardado_para_sempre()
    {
        var sha = GravarBlob("conteúdo de um jar qualquer"u8.ToArray());
        using var factory = ComStore();
        using var client = factory.CreateClient();

        var resposta = await client.GetAsync($"/api/v1/blobs/{sha}", TestContext.Current.CancellationToken);

        resposta.StatusCode.ShouldBe(HttpStatusCode.OK);

        var cache = resposta.Headers.CacheControl.ShouldNotBeNull();
        cache.Public.ShouldBeTrue();
        cache.MaxAge.ShouldBe(TimeSpan.FromDays(365));

        // 'immutable' é o que dispensa a revalidação. Sem ele o cache obedece o
        // max-age mas revalida assim que o usuário recarrega.
        cache.Extensions.ShouldContain(e => e.Name == "immutable");
    }

    [Fact]
    public async Task ETag_e_o_proprio_hash()
    {
        var sha = GravarBlob("outro conteúdo"u8.ToArray());
        using var factory = ComStore();
        using var client = factory.CreateClient();

        var resposta = await client.GetAsync($"/api/v1/blobs/{sha}", TestContext.Current.CancellationToken);

        // O hash JÁ é a identidade do conteúdo; inventar outro validador seria
        // manter dois nomes para a mesma coisa.
        resposta.Headers.ETag!.Tag.ShouldBe($"\"{sha}\"");
    }

    [Fact]
    public async Task Range_continua_aceito_para_retomar_download()
    {
        var sha = GravarBlob(Encoding.UTF8.GetBytes(new string('x', 5000)));
        using var factory = ComStore();
        using var client = factory.CreateClient();

        var requisicao = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/blobs/{sha}");
        requisicao.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(1000, 1999);

        var resposta = await client.SendAsync(requisicao, TestContext.Current.CancellationToken);

        // O cache longo não pode ter custado o resume: sem Range, um download de
        // 400 MB interrompido recomeça do zero.
        resposta.StatusCode.ShouldBe(HttpStatusCode.PartialContent);
        resposta.Content.Headers.ContentLength.ShouldBe(1000);
    }

    [Fact]
    public async Task Resposta_de_erro_nao_e_guardada()
    {
        using var factory = ComStore();
        using var client = factory.CreateClient();

        var ausente = new string('0', 64);
        var resposta = await client.GetAsync($"/api/v1/blobs/{ausente}", TestContext.Current.CancellationToken);

        resposta.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Guardar um 404 por um ano significaria que o blob enviado depois ficava
        // invisível para quem já tinha perguntado por ele.
        (resposta.Headers.CacheControl?.MaxAge).ShouldBeNull();
    }

    private TcMineAppFactory ComStore() => new(settings: ("BlobStorage:RootPath", _raiz));

    /// <summary>Escreve o arquivo no layout shard do store e devolve o hash.</summary>
    private string GravarBlob(byte[] conteudo)
    {
        var sha = Convert.ToHexStringLower(SHA256.HashData(conteudo));

        var caminho = Path.Combine(_raiz, sha[..2], sha[2..4], sha);
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        File.WriteAllBytes(caminho, conteudo);

        return sha;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_raiz))
                Directory.Delete(_raiz, recursive: true);
        }
        catch (IOException)
        {
            // Limpeza não reprova teste; o SO recolhe a pasta temporária.
        }
    }
}
