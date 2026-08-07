using Microsoft.Extensions.Logging.Abstractions;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

public sealed class ModpackIngestionServiceTests
{
    [Fact]
    public async Task Puxa_dependencias_requeridas_e_ignora_opcionais()
    {
        var version = NewDraftVersion();
        var repo = new FakeModpackRepository { Version = version };

        // A precisa de B (requerida) e C (opcional). B precisa de D (requerida).
        var resolver = new FakeResolver(new Dictionary<string, IReadOnlyList<ModDependency>>
        {
            ["A"] = [Req("B"), Opt("C")], ["B"] = [Req("D")], ["C"] = [], ["D"] = []
        });

        var service = NewService(repo, resolver);

        await service.IngestAsync(version.Id, [Item("A")], CancellationToken.None);

        var slugs = version.Files.Select(f => f.ProjectSlug!).OrderBy(s => s).ToArray();
        Assert.Equal(["A", "B", "D"], slugs); // opcional C fora; transitiva D dentro
        Assert.Equal(ModpackVersionState.Draft, version.State);
    }

    [Fact]
    public async Task Nao_entra_em_loop_com_dependencia_circular()
    {
        var version = NewDraftVersion();
        var repo = new FakeModpackRepository { Version = version };

        var resolver = new FakeResolver(new Dictionary<string, IReadOnlyList<ModDependency>>
        {
            ["A"] = [Req("B")], ["B"] = [Req("A")] // ciclo
        });

        var service = NewService(repo, resolver);

        await service.IngestAsync(version.Id, [Item("A")], CancellationToken.None);

        Assert.Equal(2, version.Files.Count);
        Assert.Equal(ModpackVersionState.Draft, version.State);
    }

    [Fact]
    public async Task Falha_de_dependencia_marca_a_versao_como_falha()
    {
        var version = NewDraftVersion();
        var repo = new FakeModpackRepository { Version = version };

        // A precisa de X, que o resolver não encontra.
        var resolver = new FakeResolver(
            new Dictionary<string, IReadOnlyList<ModDependency>> { ["A"] = [Req("X")] },
            ["X"]);

        var service = NewService(repo, resolver);

        await service.IngestAsync(version.Id, [Item("A")], CancellationToken.None);

        Assert.Equal(ModpackVersionState.Failed, version.State);
    }

    // ---- Fixtures ----

    private static ModpackIngestionService NewService(FakeModpackRepository repo, FakeResolver resolver)
    {
        return new ModpackIngestionService(repo, new FakeBlobStore(), [resolver], new FakeDownloader(),
            NullLogger<ModpackIngestionService>.Instance);
    }

    private static ModpackVersion NewDraftVersion() =>
        new() { ModpackId = Guid.CreateVersion7(), Version = "1.0", LoaderVersion = "21.1.234" };

    private static ModIngestionItem Item(string projectId) =>
        new(ModFileOrigin.Modrinth, projectId, null, FileSide.Both);

    private static ModDependency Req(string id) => new(id, ModDependencyKind.Required);

    private static ModDependency Opt(string id) => new(id, ModDependencyKind.Optional);

    // ---- Fakes ----

    private sealed class FakeResolver(
        Dictionary<string, IReadOnlyList<ModDependency>> deps,
        HashSet<string>? notFound = null) : IModResolver
    {
        private readonly HashSet<string> _notFound = notFound ?? [];

        public ModFileOrigin Origin => ModFileOrigin.Modrinth;
        public ValueTask<bool> IsAvailableAsync(CancellationToken ct) => ValueTask.FromResult(true);

        public Task<ModResolution> ResolveAsync(ModRequest request, CancellationToken ct)
        {
            if (_notFound.Contains(request.ProjectId))
                return Task.FromResult<ModResolution>(new ModResolution.NotFound("não encontrado"));

            var d = deps.TryGetValue(request.ProjectId, out var found) ? found : [];
            var resolved = new ModResolution.Resolved(
                $"{request.ProjectId}-v1",
                $"{request.ProjectId}.jar",
                null,
                10,
                new Uri($"https://example.test/{request.ProjectId}.jar"),
                d);

            return Task.FromResult<ModResolution>(resolved);
        }
    }

    private sealed class FakeDownloader : IModDownloader
    {
        public Task<Stream> OpenAsync(Uri url, CancellationToken ct) =>
            Task.FromResult<Stream>(new MemoryStream(new byte[10]));
    }

    private sealed class FakeBlobStore : IBlobStore
    {
        public Task<bool> ExistsAsync(string sha256, CancellationToken ct) => Task.FromResult(false);

        // Sha fixo: os arquivos só diferem por ProjectSlug/Path, então o
        // conteúdo idêntico não atrapalha a dedup (que também olha o slug).
        public Task<string>
            PutAsync(Stream content, string? expectedSha256, string contentType, CancellationToken ct) =>
            Task.FromResult(new string('a', 64));

        public Task<Stream> OpenAsync(string sha256, CancellationToken ct) =>
            Task.FromResult<Stream>(new MemoryStream(new byte[10]));

        public Task<Uri?> TryGetDirectUrlAsync(string sha256, TimeSpan lifetime, CancellationToken ct) =>
            Task.FromResult<Uri?>(null);

        public Task<string?> TryGetLocalPathAsync(string sha256, CancellationToken ct) =>
            throw new NotImplementedException();
    }

    private sealed class FakeModpackRepository : IModpackRepository
    {
        public ModpackVersion? Version { get; init; }

        public Task<ModpackVersion?> GetVersionAsync(Guid versionId, CancellationToken ct) => Task.FromResult(Version);

        public Task RemoveVersionAsync(Guid versionId, CancellationToken ct) => throw new NotImplementedException();

        public Task UpdateVersionAsync(ModpackVersion version, CancellationToken ct) => Task.CompletedTask;

        public Task<Modpack?> GetWithVersionsAsync(Guid id, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task RemoveFileAsync(Guid versionId, Guid fileId, CancellationToken ct) => Task.CompletedTask;

        public Task UpdateAsync(Modpack modpack, CancellationToken ct) => throw new NotImplementedException();

        public Task<bool> SlugExistsAsync(string slug, CancellationToken ct) => throw new NotImplementedException();

        public Task<Modpack?> GetByIdAsync(Guid id, CancellationToken ct)
        {
            // O caso de uso lê MinecraftVersion/Loader do modpack agora.
            return Task.FromResult<Modpack?>(new Modpack
            {
                Slug = "test", Name = "Test", MinecraftVersion = "1.21.1", Loader = ModLoader.NeoForge
            });
        }

        public Task<IReadOnlyList<Modpack>> ListAsync(CancellationToken ct) => throw new NotImplementedException();

        public Task<IReadOnlyList<ModpackVersion>> ListVersionsAsync(Guid modpackId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task RemoveAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();

        public Task CreateAsync(Modpack modpack, CancellationToken ct) => throw new NotImplementedException();

        public Task AddVersionAsync(ModpackVersion version, CancellationToken ct) =>
            throw new NotImplementedException();
    }
}
