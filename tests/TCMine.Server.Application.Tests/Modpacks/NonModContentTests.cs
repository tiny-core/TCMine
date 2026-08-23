using Microsoft.Extensions.Logging.Abstractions;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

/// <summary>
///     Nem tudo o que um modpack lista é mod.
///     O All the Mods 10 traz 481 mods e 4 shaderpacks no mesmo manifesto. Um
///     shaderpack gravado em <c>mods/</c> derruba o jogo no arranque: o loader
///     tenta carregá-lo como mod e falha com um erro que não aponta para cá.
///     A pasta é decidida por quem resolve — só ele conhece a categoria do
///     projeto na origem —, e estes testes travam a ingestão respeitando essa
///     decisão em vez de fixar <c>mods/</c>.
/// </summary>
public sealed class NonModContentTests
{
    [Fact]
    public async Task Shaderpack_vai_para_a_pasta_de_shaders()
    {
        var version = NovaVersao();

        await Ingerir(version, new ResolveEm("shaderpacks", "complementary.zip", FileSide.ClientOnly));

        var arquivo = Assert.Single(version.Files);
        Assert.Equal("shaderpacks/complementary.zip", arquivo.Path);
    }

    [Fact]
    public async Task Shaderpack_fica_marcado_como_de_cliente()
    {
        // O que decide se o arquivo vai para o container do servidor. Um shader
        // lá é peso morto que ninguém lê — e são quatro num pack como o ATM10.
        var version = NovaVersao();

        await Ingerir(version, new ResolveEm("shaderpacks", "complementary.zip", FileSide.ClientOnly));

        Assert.Equal(FileSide.ClientOnly, version.Files.Single().Side);
    }

    [Fact]
    public async Task Mod_continua_indo_para_mods()
    {
        // A regressão que importa: o caminho comum não pode mudar.
        var version = NovaVersao();

        await Ingerir(version, new ResolveEm("mods", "jei-1.21.1.jar", null));

        Assert.Equal("mods/jei-1.21.1.jar", version.Files.Single().Path);
    }

    private static async Task Ingerir(ModpackVersion version, IModResolver resolver)
    {
        await new ModpackIngestionService(
                new FakeRepo(version), new FakeBlob(), [resolver], new FakeDownloader(),
                new FakeJarInspector(), new FakeJobProgress(),
                NullLogger<ModpackIngestionService>.Instance)
            .IngestAsync(version.Id, [new ModIngestionItem(ModFileOrigin.CurseForge, "306612", null, FileSide.Both)],
                CancellationToken.None);
    }

    private static ModpackVersion NovaVersao() =>
        new() { ModpackId = Guid.CreateVersion7(), Version = "1.0.0", LoaderVersion = "21.1.100" };

    private sealed class ResolveEm(string pasta, string arquivo, FileSide? lado) : IModResolver
    {
        public ModFileOrigin Origin => ModFileOrigin.CurseForge;
        public ValueTask<bool> IsAvailableAsync(CancellationToken ct) => ValueTask.FromResult(true);

        public Task<ModResolution> ResolveAsync(ModRequest request, CancellationToken ct) =>
            Task.FromResult<ModResolution>(new ModResolution.Resolved(
                "999",
                arquivo,
                null,
                10,
                new Uri("https://exemplo/arquivo"),
                [],
                null,
                lado,
                pasta));
    }

    private sealed class FakeRepo(ModpackVersion version) : FakeModpackRepositoryBase
    {
        public override Task<ModpackVersion?> GetVersionAsync(Guid versionId, CancellationToken ct) =>
            Task.FromResult<ModpackVersion?>(version);

        public override Task<Modpack?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<Modpack?>(new Modpack
            {
                Name = "Pack", Slug = "pack", MinecraftVersion = "1.21.1", Loader = ModLoader.NeoForge
            });

        public override Task UpdateVersionAsync(ModpackVersion v, CancellationToken ct) => Task.CompletedTask;
        public override Task SaveVersionStateAsync(ModpackVersion v, CancellationToken ct) => Task.CompletedTask;

        public override Task AddFilesAsync(
            Guid versionId, IReadOnlyList<ModpackFile> files, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeBlob : FakeBlobStoreBase
    {
        public override Task<string> PutAsync(
            Stream content, string? expectedSha256, string contentType, CancellationToken ct) =>
            Task.FromResult(new string('b', 64));

        // A ingestão relê o blob para inspecionar o jar antes de gravar a linha.
        public override Task<Stream> OpenAsync(string sha256, CancellationToken ct) =>
            Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
    }

    private sealed class FakeDownloader : IModDownloader
    {
        public Task<Stream> OpenAsync(Uri url, CancellationToken ct) =>
            Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
    }
}
