using Microsoft.Extensions.Logging.Abstractions;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

/// <summary>
///     Reingerir um mod que não mudou não pode baixá-lo de novo.
///     O blob store é endereçado por conteúdo, então rebaixar nunca duplicou
///     espaço em disco — mas os bytes desciam mesmo assim, para serem
///     descartados depois de hasheados. Num pack de centenas de mods isso é um
///     gigabyte e meio de rede por reimportação.
///     A identidade que resolve é o <c>OriginReference</c>: o id da release
///     fixada na origem. Se ele bate, é o mesmo arquivo lá.
/// </summary>
public sealed class SkipUnchangedDownloadTests
{
    [Fact]
    public async Task Nao_baixa_de_novo_a_mesma_release()
    {
        var version = ComArquivo(originReference: "999");
        var downloader = new ContaDownloads();

        await Ingerir(version, downloader);

        downloader.Chamadas.ShouldBe(0, "a release fixada é a mesma que já está gravada");
        version.Files.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Baixa_quando_a_release_mudou()
    {
        // A regressão que importa: atualizar um mod tem de continuar funcionando.
        // O arquivo gravado aponta para outra release, então o novo desce.
        var version = ComArquivo(originReference: "111");
        var downloader = new ContaDownloads();

        await Ingerir(version, downloader);

        downloader.Chamadas.ShouldBe(1);
    }

    [Fact]
    public async Task Baixa_quando_o_mod_ainda_nao_existe()
    {
        var version = new ModpackVersion
        {
            ModpackId = Guid.CreateVersion7(), Version = "1.0.0", LoaderVersion = "21.1.100"
        };
        var downloader = new ContaDownloads();

        await Ingerir(version, downloader);

        downloader.Chamadas.ShouldBe(1);
    }

    private static async Task Ingerir(ModpackVersion version, ContaDownloads downloader)
    {
        await new ModpackIngestionService(
                new FakeRepo(version), new FakeBlob(), [new ResolveFixo()], downloader,
                new FakeJarInspector(), new FakeJobProgress(),
                NullLogger<ModpackIngestionService>.Instance)
            .IngestAsync(
                version.Id,
                [new ModIngestionItem(ModFileOrigin.CurseForge, "jei", null, FileSide.Both)],
                CancellationToken.None);
    }

    private static ModpackVersion ComArquivo(string originReference)
    {
        var version = new ModpackVersion
        {
            ModpackId = Guid.CreateVersion7(), Version = "1.0.0", LoaderVersion = "21.1.100"
        };

        version.UpsertFile(new ModpackFile
        {
            ModpackVersionId = version.Id,
            Path = "mods/jei.jar",
            Sha256 = new string('a', 64),
            SizeBytes = 10,
            Side = FileSide.Both,
            Origin = ModFileOrigin.CurseForge,
            ProjectSlug = "jei",
            OriginReference = originReference
        });

        return version;
    }

    /// <summary>Resolve sempre para a release 999.</summary>
    private sealed class ResolveFixo : IModResolver
    {
        public ModFileOrigin Origin => ModFileOrigin.CurseForge;
        public ValueTask<bool> IsAvailableAsync(CancellationToken ct) => ValueTask.FromResult(true);

        public Task<ModResolution> ResolveAsync(ModRequest request, CancellationToken ct) =>
            Task.FromResult<ModResolution>(new ModResolution.Resolved(
                "999", "jei.jar", null, 10, new Uri("https://exemplo/jei.jar"), []));
    }

    private sealed class ContaDownloads : IModDownloader
    {
        public int Chamadas { get; private set; }

        public Task<Stream> OpenAsync(Uri url, CancellationToken ct)
        {
            Chamadas++;
            return Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
        }
    }

    private sealed class FakeBlob : FakeBlobStoreBase
    {
        public override Task<string> PutAsync(
            Stream content, string? expectedSha256, string contentType, CancellationToken ct) =>
            Task.FromResult(new string('b', 64));

        public override Task<Stream> OpenAsync(string sha256, CancellationToken ct) =>
            Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
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
}
