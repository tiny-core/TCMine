using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

public sealed class PublishModpackVersionTests
{
    [Fact]
    public async Task Publica_versao_em_rascunho_com_arquivos()
    {
        var version = DraftWithFile();
        var repo = new FakeModpackRepository { Version = version };
        var notifier = new FakeHubNotifier();
        var useCase = new PublishModpackVersion(repo, notifier);

        var result = await useCase.HandleAsync(version.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(repo.Saved);
        Assert.Equal(ModpackVersionState.Ready, repo.Saved!.State);
        Assert.Equal(1, notifier.Calls); // avisou os launchers
    }

    [Fact]
    public async Task Falha_quando_a_versao_nao_existe()
    {
        var repo = new FakeModpackRepository { Version = null };
        var notifier = new FakeHubNotifier();
        var useCase = new PublishModpackVersion(repo, notifier);

        var result = await useCase.HandleAsync(Guid.CreateVersion7(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(repo.Saved);
        Assert.Equal(0, notifier.Calls);
    }

    [Fact]
    public async Task Falha_ao_publicar_versao_vazia()
    {
        var version = NewDraftVersion(); // sem arquivos
        var repo = new FakeModpackRepository { Version = version };
        var notifier = new FakeHubNotifier();
        var useCase = new PublishModpackVersion(repo, notifier);

        var result = await useCase.HandleAsync(version.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(repo.Saved); // não gravou nada
        Assert.Equal(0, notifier.Calls); // nem avisou
    }

    // ---- Fixtures ----

    private static ModpackVersion NewDraftVersion()
    {
        return new ModpackVersion
        {
            ModpackId = Guid.CreateVersion7(),
            Version = "1.0",
            LoaderVersion = "21.1.234"
        };
    }

    private static ModpackVersion DraftWithFile()
    {
        var version = NewDraftVersion();
        version.UpsertFile(new ModpackFile
        {
            ModpackVersionId = version.Id,
            ProjectSlug = "jei",
            Path = "mods/jei.jar",
            Sha256 = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            SizeBytes = 1024,
            Side = FileSide.Both
        });

        return version;
    }

    // ---- Fakes ----

    // O publish só toca GetVersionAsync e UpdateVersionAsync. Os demais membros
    // existem só para satisfazer a interface; se algum for chamado, o teste
    // quebra alto (NotImplementedException), o que é o comportamento desejado.
    private sealed class FakeModpackRepository : IModpackRepository
    {
        public ModpackVersion? Version { get; init; }
        public ModpackVersion? Saved { get; private set; }

        public Task<ModpackVersion?> GetVersionAsync(Guid versionId, CancellationToken ct)
        {
            return Task.FromResult(Version);
        }

        public Task UpdateVersionAsync(ModpackVersion version, CancellationToken ct)
        {
            Saved = version;
            return Task.CompletedTask;
        }

        public Task<Modpack?> GetWithVersionsAsync(Guid id, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<bool> SlugExistsAsync(string slug, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<Modpack?> GetByIdAsync(Guid id, CancellationToken ct)
        {
            // O caso de uso lê MinecraftVersion/Loader do modpack agora.
            return Task.FromResult<Modpack?>(new Modpack
            {
                Slug = "test",
                Name = "Test",
                MinecraftVersion = "1.21.1",
                Loader = ModLoader.NeoForge
            });
        }

        public Task<IReadOnlyList<Modpack>> ListAsync(CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<ModpackVersion>> ListVersionsAsync(Guid modpackId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task CreateAsync(Modpack modpack, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task AddVersionAsync(ModpackVersion version, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task RemoveFileAsync(Guid versionId, Guid fileId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }

    private sealed class FakeHubNotifier : IServerHubNotifier
    {
        public int Calls { get; private set; }

        public Task NotifyModpackVersionPublishedAsync(Guid modpackId, Guid versionId, CancellationToken ct)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }
}