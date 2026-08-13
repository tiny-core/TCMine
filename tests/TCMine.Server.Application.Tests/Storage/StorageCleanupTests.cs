using System.Runtime.CompilerServices;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Storage;
using TCMine.Server.Application.Tests.Fakes;

namespace TCMine.Server.Application.Tests.Storage;

/// <summary>
///     Apagar blob é a operação mais perigosa do sistema: um byte a menos quebra
///     silenciosamente toda versão que aponta para ele. Cada teste aqui é uma
///     forma de quebrar.
/// </summary>
public sealed class StorageCleanupTests
{
    private static readonly DateTimeOffset Velho = DateTimeOffset.UtcNow.AddDays(-3);
    private static readonly DateTimeOffset Novo = DateTimeOffset.UtcNow.AddMinutes(-5);

    [Fact]
    public async Task Blob_referenciado_nao_entra_na_lista()
    {
        var janitor = new FakeJanitor(Blob("aa", 100, Velho), Blob("bb", 50, Velho));
        var repo = new FakeRepo("aa");

        var report = (await new ScanStorage(janitor, repo, new FakeJobProgress()).HandleAsync(CancellationToken.None)).Value!;

        var orfao = Assert.Single(report.Orphans);
        Assert.Equal(Hash("bb"), orfao.Sha256);
        Assert.Equal(100, report.ReferencedBytes);
        Assert.Equal(150, report.TotalBytes);
    }

    [Fact]
    public async Task Blob_recente_conta_como_orfao_mas_nao_como_recuperavel()
    {
        // A ingestão grava em lotes: o blob está no disco antes da linha. Sem
        // esta guarda, uma limpeza no meio de uma importação a destruiria.
        var janitor = new FakeJanitor(Blob("cc", 70, Novo));
        var repo = new FakeRepo();

        var report = (await new ScanStorage(janitor, repo, new FakeJobProgress()).HandleAsync(CancellationToken.None)).Value!;

        Assert.Single(report.Orphans);
        Assert.Equal(0, report.ReclaimableBytes);
        Assert.Equal(1, report.TooRecentCount);
    }

    [Fact]
    public async Task Nao_apaga_blob_que_passou_a_ser_referenciado_depois_da_varredura()
    {
        // O store é endereçado por conteúdo: outro pack importado no meio-tempo
        // pode ter passado a compartilhar exatamente este blob.
        var janitor = new FakeJanitor(Blob("dd", 10, Velho));
        var repo = new FakeRepo("dd");

        var result = await new DeleteOrphanBlobs(janitor, repo, new FakeJobProgress())
            .HandleAsync([Hash("dd")], CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.Value!.Deleted);
        Assert.Equal(1, result.Value.Skipped);
        Assert.Empty(janitor.Apagados);
    }

    [Fact]
    public async Task Nao_apaga_blob_recente_mesmo_se_pedirem_explicitamente()
    {
        var janitor = new FakeJanitor(Blob("ee", 10, Novo));
        var repo = new FakeRepo();

        var result = await new DeleteOrphanBlobs(janitor, repo, new FakeJobProgress())
            .HandleAsync([Hash("ee")], CancellationToken.None);

        Assert.Equal(0, result.Value!.Deleted);
        Assert.Empty(janitor.Apagados);
    }

    [Fact]
    public async Task Apaga_o_que_e_seguro_e_soma_o_liberado()
    {
        var janitor = new FakeJanitor(Blob("ff", 4096, Velho));
        var repo = new FakeRepo();

        var result = await new DeleteOrphanBlobs(janitor, repo, new FakeJobProgress())
            .HandleAsync([Hash("ff")], CancellationToken.None);

        Assert.Equal(1, result.Value!.Deleted);
        Assert.Equal(4096, result.Value.FreedBytes);
        Assert.Equal([Hash("ff")], janitor.Apagados);
    }

    // ---- Fixtures ----

    private static string Hash(string prefix) => prefix + new string('0', 64 - prefix.Length);

    private static StoredBlob Blob(string prefix, long size, DateTimeOffset created) =>
        new(Hash(prefix), size, created);

    // ---- Fakes ----

    private sealed class FakeJanitor(params StoredBlob[] blobs) : IBlobJanitor
    {
        public List<string> Apagados { get; } = [];

        public async IAsyncEnumerable<StoredBlob> EnumerateAsync(
            [EnumeratorCancellation] CancellationToken ct)
        {
            foreach (var blob in blobs)
            {
                yield return blob;
                await Task.Yield();
            }
        }

        public Task<bool> DeleteAsync(string sha256, CancellationToken ct)
        {
            Apagados.Add(sha256);
            return Task.FromResult(true);
        }
    }

    private sealed class FakeRepo(params string[] referenced) : FakeModpackRepositoryBase
    {
        public override Task<IReadOnlySet<string>> ListReferencedHashesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlySet<string>>(
                referenced.Select(Hash).ToHashSet(StringComparer.OrdinalIgnoreCase));
    }
}
