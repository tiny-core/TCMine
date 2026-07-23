using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TCMine.Server.Infrastructure.Storage;

namespace TCMine.Server.Application.Tests.Storage;

/// <summary>
///     IDisposable para garantir a limpeza: o xunit cria uma instância da classe
///     por teste e descarta ao final de cada um.
/// </summary>
public sealed class FileSystemBlobStoreTests : IDisposable
{
    // SHA-256 de "conteúdo de teste"
    private const string HashConhecido =
        "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    private readonly string _raiz = Path.Combine(
        Path.GetTempPath(),
        "tcmine-testes-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_raiz))
            Directory.Delete(_raiz, true);
    }

    private FileSystemBlobStore CriarStore()
    {
        return new FileSystemBlobStore(Options.Create(new BlobStorageOptions { RootPath = _raiz }),
            NullLogger<FileSystemBlobStore>.Instance);
    }

    private static Stream Conteudo(string texto)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(texto));
    }

    [Fact]
    public async Task Grava_e_devolve_o_hash_do_conteudo()
    {
        var store = CriarStore();

        var hash = await store.PutAsync(Conteudo("olá"), null, "text/plain", TestContext.Current.CancellationToken);

        hash.Length.ShouldBe(64);
        (await store.ExistsAsync(hash, TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task Conteudo_identico_gera_o_mesmo_hash()
    {
        // A base da deduplicação: dois uploads do mesmo mod ocupam um arquivo.
        var store = CriarStore();
        var ct = TestContext.Current.CancellationToken;

        var primeiro = await store.PutAsync(Conteudo("mesmo"), null, "text/plain", ct);
        var segundo = await store.PutAsync(Conteudo("mesmo"), null, "text/plain", ct);

        segundo.ShouldBe(primeiro);
    }

    [Fact]
    public async Task Rejeita_conteudo_com_hash_divergente()
    {
        var store = CriarStore();

        await Should.ThrowAsync<InvalidDataException>(async () =>
            await store.PutAsync(
                Conteudo("conteúdo real"),
                new string('a', 64),
                "text/plain",
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Nao_deixa_arquivo_para_tras_quando_o_hash_falha()
    {
        // Se sobrasse um temporário a cada download corrompido, o disco
        // encheria em silêncio.
        var store = CriarStore();

        try
        {
            await store.PutAsync(Conteudo("x"), new string('b', 64), "text/plain",
                TestContext.Current.CancellationToken);
        }
        catch (InvalidDataException)
        {
        }

        Directory.GetFiles(Path.Combine(_raiz, ".tmp")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Le_de_volta_o_que_foi_gravado()
    {
        var store = CriarStore();
        var ct = TestContext.Current.CancellationToken;

        var hash = await store.PutAsync(Conteudo("recuperável"), null, "text/plain", ct);

        await using var stream = await store.OpenAsync(hash, ct);
        using var reader = new StreamReader(stream);

        (await reader.ReadToEndAsync(ct)).ShouldBe("recuperável");
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("nao-e-hash")]
    [InlineData("zzzz")]
    public async Task Rejeita_hash_malformado(string invalido)
    {
        // Este valor chega por requisição HTTP. Sem validação, um hash com
        // ".." leria qualquer arquivo do servidor.
        var store = CriarStore();

        await Should.ThrowAsync<ArgumentException>(async () =>
            await store.ExistsAsync(invalido, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Falha_ao_abrir_blob_inexistente()
    {
        var store = CriarStore();

        await Should.ThrowAsync<FileNotFoundException>(async () =>
            await store.OpenAsync(HashConhecido, TestContext.Current.CancellationToken));
    }
}