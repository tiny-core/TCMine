using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

public sealed class ArchiveRestoreModpackVersionTests
{
    [Fact]
    public async Task Arquiva_versao_publicada()
    {
        var version = ReadyVersion();
        var repo = new FakeRepo { Version = version };
        var useCase = new ArchiveModpackVersion(repo);

        var result = await useCase.HandleAsync(version.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(ModpackVersionState.Archived, repo.Saved!.State);
    }

    [Fact]
    public async Task Nao_arquiva_rascunho()
    {
        // A máquina de estados recusa arquivar o que não está publicado.
        var version = DraftVersion();
        var repo = new FakeRepo { Version = version };
        var useCase = new ArchiveModpackVersion(repo);

        var result = await useCase.HandleAsync(version.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(repo.Saved);
    }

    [Fact]
    public async Task Restaura_versao_arquivada_para_ready()
    {
        var version = ReadyVersion();
        version.Archive();
        var repo = new FakeRepo { Version = version };
        var useCase = new RestoreModpackVersion(repo);

        var result = await useCase.HandleAsync(version.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(ModpackVersionState.Ready, repo.Saved!.State);
    }

    [Fact]
    public async Task Nao_restaura_versao_que_nao_esta_arquivada()
    {
        var version = ReadyVersion(); // publicada, não arquivada
        var repo = new FakeRepo { Version = version };
        var useCase = new RestoreModpackVersion(repo);

        var result = await useCase.HandleAsync(version.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(repo.Saved);
    }

    [Fact]
    public async Task Falha_quando_a_versao_nao_existe()
    {
        var repo = new FakeRepo { Version = null };
        var useCase = new ArchiveModpackVersion(repo);

        var result = await useCase.HandleAsync(Guid.CreateVersion7(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(repo.Saved);
    }

    // ---- Fixtures ----

    private static ModpackVersion DraftVersion() =>
        new() { ModpackId = Guid.CreateVersion7(), Version = "1.0", LoaderVersion = "21.1.234" };

    private static ModpackVersion ReadyVersion()
    {
        var version = DraftVersion();
        version.UpsertFile(new ModpackFile
        {
            ModpackVersionId = version.Id,
            ProjectSlug = "jei",
            Path = "mods/jei.jar",
            Sha256 = new string('a', 64),
            SizeBytes = 1024,
            Side = FileSide.Both
        });
        version.MarkResolving();
        version.MarkReady();
        return version;
    }

    // ---- Fake ----

    // Archive/Restore só tocam GetVersionAsync e UpdateVersionAsync; o resto
    // lança se for chamado (nunca deveria).
    private sealed class FakeRepo : IModpackRepository
    {
        public ModpackVersion? Version { get; init; }
        public ModpackVersion? Saved { get; private set; }

        public Task<ModpackVersion?> GetVersionAsync(Guid versionId, CancellationToken ct) =>
            Task.FromResult(Version);

        public Task UpdateVersionAsync(ModpackVersion version, CancellationToken ct)
        {
            Saved = version;
            return Task.CompletedTask;
        }

        public Task<bool> SlugExistsAsync(string slug, CancellationToken ct) => throw new NotImplementedException();
        public Task<Modpack?> GetByIdAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
        public Task RemoveAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
        public Task RemoveVersionAsync(Guid versionId, CancellationToken ct) => throw new NotImplementedException();
        public Task UpdateAsync(Modpack modpack, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<Modpack>> ListAsync(CancellationToken ct) => throw new NotImplementedException();

        public Task<IReadOnlyList<ModpackVersion>> ListVersionsAsync(Guid modpackId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task CreateAsync(Modpack modpack, CancellationToken ct) => throw new NotImplementedException();
        public Task AddVersionAsync(ModpackVersion version, CancellationToken ct) => throw new NotImplementedException();
        public Task<Modpack?> GetWithVersionsAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();

        public Task RemoveFileAsync(Guid versionId, Guid fileId, CancellationToken ct) =>
            throw new NotImplementedException();
    }
}
