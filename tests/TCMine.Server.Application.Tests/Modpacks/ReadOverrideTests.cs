using System.Text;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

public sealed class ReadOverrideTests
{
    [Fact]
    public async Task Le_arquivo_de_texto_normal()
    {
        var content = Encoding.UTF8.GetBytes("greeting=olá\n");
        var (useCase, _) = Build("config/mod.toml", content);

        var result = await useCase.HandleAsync(Guid.Empty, "config/mod.toml", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("greeting=olá\n", result.Value!.Text);
        Assert.False(result.Value.IsBinary);
    }

    [Fact]
    public async Task Recusa_abrir_binario_no_editor()
    {
        // Um PNG começa com \x89PNG\r\n\x1a\n e tem bytes zero logo em seguida.
        // Despejado no Monaco, travava a aba do admin.
        var png = new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13 };
        var (useCase, _) = Build("config/icon.png", png);

        var result = await useCase.HandleAsync(Guid.Empty, "config/icon.png", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.Value!.Text);
        Assert.True(result.Value.IsBinary);
    }

    [Fact]
    public async Task Recusa_abrir_arquivo_grande_sem_sequer_ler_o_blob()
    {
        var (useCase, store) = Build("kubejs/enorme.js", "// só texto"u8.ToArray(), 5 * 1024 * 1024);

        var result = await useCase.HandleAsync(Guid.Empty, "kubejs/enorme.js", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.Value!.Text);

        // O corte pelo tamanho registrado evita puxar megabytes do content store.
        Assert.False(store.Aberto);
    }

    // ---- Fixtures ----

    private static (ReadOverride UseCase, FakeBlobStore Store) Build(
        string path, byte[] content, long? tamanhoDeclarado = null)
    {
        var version = new ModpackVersion { ModpackId = Guid.Empty, Version = "1.0.0", LoaderVersion = "1" };
        version.UpsertFile(new ModpackFile
        {
            ModpackVersionId = version.Id,
            Path = path,
            Sha256 = new string('a', 64),
            SizeBytes = tamanhoDeclarado ?? content.Length,
            Side = FileSide.Both,
            Origin = ModFileOrigin.Override,
            ProjectSlug = $"override:{path}"
        });

        var store = new FakeBlobStore(content);
        return (new ReadOverride(new FakeRepo(version), store), store);
    }

    // ---- Fakes ----

    private sealed class FakeBlobStore(byte[] content) : FakeBlobStoreBase
    {
        public bool Aberto { get; private set; }

        public override Task<Stream> OpenAsync(string sha256, CancellationToken ct)
        {
            Aberto = true;
            return Task.FromResult<Stream>(new MemoryStream(content));
        }

        public override Task<bool> ExistsAsync(string sha256, CancellationToken ct) => Task.FromResult(true);

        public override Task<Uri?> TryGetDirectUrlAsync(string sha256, TimeSpan lifetime, CancellationToken ct) =>
            Task.FromResult<Uri?>(null);

        public override Task<string?> TryGetLocalPathAsync(string sha256, CancellationToken ct) =>
            Task.FromResult<string?>(null);
    }

    private sealed class FakeRepo(ModpackVersion version) : FakeModpackRepositoryBase
    {
        public override Task<ModpackVersion?> GetVersionAsync(Guid versionId, CancellationToken ct) =>
            Task.FromResult<ModpackVersion?>(version);

        public override Task UpdateVersionAsync(ModpackVersion v, CancellationToken ct) => Task.CompletedTask;
        public override Task RemoveFileAsync(Guid versionId, Guid fileId, CancellationToken ct) => Task.CompletedTask;
    }
}
