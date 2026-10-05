using Microsoft.Extensions.Logging.Abstractions;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

/// <summary>
///     Um mod do Modrinth tem UMA identidade: o id do projeto. A busca gravava o
///     slug, o pack e as dependências gravavam o id — o mesmo mod pelos dois
///     caminhos virava dois .jar em mods/, e o jogo não sobe assim.
/// </summary>
public sealed class ModrinthIdentityTests
{
    [Fact]
    public async Task Mod_gravado_pelo_slug_e_substituido_pela_linha_do_id()
    {
        var version = new ModpackVersion
        {
            ModpackId = Guid.CreateVersion7(), Version = "1.0.0", LoaderVersion = "21.1.100"
        };
        version.UpsertFile(new ModpackFile
        {
            ModpackVersionId = version.Id,
            Path = "mods/jei-1.0.jar",
            Sha256 = new string('a', 64),
            SizeBytes = 10,
            Side = FileSide.Both,
            Origin = ModFileOrigin.Modrinth,
            ProjectSlug = "jei", // como a busca gravava antes
            OriginReference = "velha"
        });

        await new ModpackIngestionService(
                new Repo(version), new Blob(), [new ResolveComId()], new Downloader(),
                new FakeJarInspector(), new FakeJobProgress(),
                NullLogger<ModpackIngestionService>.Instance)
            .IngestAsync(
                version.Id,
                [new ModIngestionItem(ModFileOrigin.Modrinth, "jei", null, FileSide.Both)],
                CancellationToken.None);

        var file = version.Files.ShouldHaveSingleItem();
        file.ProjectSlug.ShouldBe("u6dRKJwZ");
        file.OriginReference.ShouldBe("nova");
    }

    private sealed class ResolveComId : IModResolver
    {
        public ModFileOrigin Origin => ModFileOrigin.Modrinth;
        public ValueTask<bool> IsAvailableAsync(CancellationToken ct) => ValueTask.FromResult(true);

        public Task<ModResolution> ResolveAsync(ModRequest request, CancellationToken ct) =>
            Task.FromResult<ModResolution>(new ModResolution.Resolved(
                "nova", "jei-2.0.jar", null, 3, new Uri("https://exemplo/jei.jar"), [],
                ProjectId: "u6dRKJwZ"));
    }

    private sealed class Downloader : IModDownloader
    {
        public Task<Stream> OpenAsync(Uri url, CancellationToken ct) =>
            Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
    }

    private sealed class Blob : FakeBlobStoreBase
    {
        public override Task<string> PutAsync(
            Stream content, string? expectedSha256, string contentType, CancellationToken ct) =>
            Task.FromResult(new string('b', 64));

        public override Task<Stream> OpenAsync(string sha256, CancellationToken ct) =>
            Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
    }

    private sealed class Repo(ModpackVersion version) : FakeModpackRepositoryBase
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
        public override Task RemoveFileAsync(Guid versionId, Guid fileId, CancellationToken ct) => Task.CompletedTask;

        public override Task AddFilesAsync(
            Guid versionId, IReadOnlyList<ModpackFile> files, CancellationToken ct) => Task.CompletedTask;
    }
}
