using Microsoft.Extensions.Logging.Abstractions;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

/// <summary>
///     A ingestão segue BANCO → DISCO → REDE. Um arquivo que outro modpack já
///     trouxe (mesma release na origem) e cujos bytes estão no blob store não
///     custa nem uma consulta à origem, nem um download.
/// </summary>
public sealed class LocalFirstIngestionTests
{
    private const string Sha = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    [Fact]
    public async Task Release_conhecida_com_blob_local_nao_consulta_a_origem()
    {
        var cenario = new Cenario(Conhecido("lib-a"));

        var version = await cenario.Ingerir("999");

        cenario.Resolver.Chamadas.ShouldBe(0);
        cenario.Downloads.ShouldBe(0);
        var file = version.Files.Single(f => f.ProjectSlug == "jei");
        file.Sha256.ShouldBe(Sha);
        file.OriginReference.ShouldBe("999");

        // As dependências gravadas viram trabalho, como viriam da origem: a
        // biblioteca foi pedida (e ESTA sim foi à origem, que não a conhecia).
        cenario.Resolver.Pedidos.ShouldBe(["lib-a"]);
    }

    [Fact]
    public async Task Blob_ausente_no_disco_cai_para_a_rede()
    {
        var cenario = new Cenario(Conhecido(""), false);

        await cenario.Ingerir("999");

        cenario.Resolver.Chamadas.ShouldBe(1);
        cenario.Downloads.ShouldBe(1);
    }

    [Fact]
    public async Task Arquivo_sem_dependencias_gravadas_consulta_a_origem_mas_nao_baixa()
    {
        // Linha de antes do campo existir: não sabemos o que o mod exige, então
        // a origem é perguntada — mas os bytes já estão aqui, e não descem.
        var cenario = new Cenario(Conhecido(null));

        var version = await cenario.Ingerir("999");

        cenario.Resolver.Chamadas.ShouldBe(1);
        cenario.Downloads.ShouldBe(0);
        version.Files.Single().RequiredDependencies.ShouldBe("");
    }

    [Fact]
    public async Task Sem_release_fixada_a_origem_decide_e_o_disco_evita_o_download()
    {
        var cenario = new Cenario(Conhecido(""));

        await cenario.Ingerir(null);

        cenario.Resolver.Chamadas.ShouldBe(1);
        cenario.Downloads.ShouldBe(0);
    }

    private static ModpackFile Conhecido(string? dependencias) => new()
    {
        ModpackVersionId = Guid.CreateVersion7(), // outra versão, outro modpack
        Path = "mods/jei.jar",
        Sha256 = Sha,
        SizeBytes = 10,
        Side = FileSide.Both,
        Origin = ModFileOrigin.CurseForge,
        ProjectSlug = "jei",
        OriginReference = "999",
        RequiredDependencies = dependencias
    };

    private sealed class Cenario(ModpackFile? conhecido, bool blobExiste = true)
    {
        private readonly ContaDownloads _downloader = new();
        public Resolver Resolver { get; } = new();
        public int Downloads => _downloader.Chamadas;

        public async Task<ModpackVersion> Ingerir(string? fileId)
        {
            var version = new ModpackVersion
            {
                ModpackId = Guid.CreateVersion7(), Version = "1.0.0", LoaderVersion = "21.1.100"
            };

            await new ModpackIngestionService(
                    new Repo(version, conhecido), new Blob(blobExiste), [Resolver], _downloader,
                    new FakeJarInspector(), new FakeJobProgress(),
                    NullLogger<ModpackIngestionService>.Instance)
                .IngestAsync(
                    version.Id,
                    [new ModIngestionItem(ModFileOrigin.CurseForge, "jei", fileId, FileSide.Both)],
                    CancellationToken.None);

            return version;
        }
    }

    /// <summary>Resolve "jei" na release 999; qualquer outro projeto não existe.</summary>
    private sealed class Resolver : IModResolver
    {
        public int Chamadas { get; private set; }
        public List<string> Pedidos { get; } = [];
        public ModFileOrigin Origin => ModFileOrigin.CurseForge;
        public ValueTask<bool> IsAvailableAsync(CancellationToken ct) => ValueTask.FromResult(true);

        public Task<ModResolution> ResolveAsync(ModRequest request, CancellationToken ct)
        {
            if (request.ProjectId != "jei")
            {
                Pedidos.Add(request.ProjectId);
                return Task.FromResult<ModResolution>(new ModResolution.NotFound("não existe"));
            }

            Chamadas++;
            return Task.FromResult<ModResolution>(new ModResolution.Resolved(
                "999", "jei.jar", null, 10, new Uri("https://exemplo/jei.jar"), []));
        }
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

    private sealed class Blob(bool existe) : FakeBlobStoreBase
    {
        public override Task<bool> ExistsAsync(string sha256, CancellationToken ct) => Task.FromResult(existe);

        public override Task<string> PutAsync(
            Stream content, string? expectedSha256, string contentType, CancellationToken ct) =>
            Task.FromResult(new string('b', 64));

        public override Task<Stream> OpenAsync(string sha256, CancellationToken ct) =>
            Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
    }

    private sealed class Repo(ModpackVersion version, ModpackFile? conhecido) : FakeModpackRepositoryBase
    {
        public override Task<ModpackVersion?> GetVersionAsync(Guid versionId, CancellationToken ct) =>
            Task.FromResult<ModpackVersion?>(version);

        public override Task<Modpack?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<Modpack?>(new Modpack
            {
                Name = "Pack", Slug = "pack", MinecraftVersion = "1.21.1", Loader = ModLoader.NeoForge
            });

        public override Task<ModpackFile?> FindIngestedFileAsync(
            ModFileOrigin origin, string originReference, CancellationToken ct) =>
            Task.FromResult(conhecido?.Origin == origin && conhecido.OriginReference == originReference
                ? conhecido
                : null);

        public override Task UpdateVersionAsync(ModpackVersion v, CancellationToken ct) => Task.CompletedTask;
        public override Task SaveVersionStateAsync(ModpackVersion v, CancellationToken ct) => Task.CompletedTask;

        public override Task AddFilesAsync(
            Guid versionId, IReadOnlyList<ModpackFile> files, CancellationToken ct) => Task.CompletedTask;
    }
}
