using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

public sealed class CheckModpackVersionUpdatesTests
{
    [Fact]
    public async Task Retorna_apenas_os_mods_com_versao_mais_nova()
    {
        var version = Fakes.NewDraftVersion();
        version.UpsertFile(Fakes.ModrinthFile(version, "jei", "jei-v1"));
        version.UpsertFile(Fakes.ModrinthFile(version, "sodium", "sodium-v1"));

        var repo = new FakeModpackRepository();
        repo.Seed(version);

        // O resolver diz: jei tem v2 (novo); sodium continua em v1 (igual).
        var resolver = new FakeResolver(new Dictionary<string, string>
        {
            ["jei"] = "jei-v2", ["sodium"] = "sodium-v1"
        });

        var useCase = new CheckModpackVersionUpdates(repo, [resolver]);
        var result = await useCase.HandleAsync(version.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        var update = Assert.Single(result.Value!); // só o jei
        Assert.Equal("jei", update.ProjectSlug);
        Assert.Equal("jei-v1", update.CurrentVersionId);
        Assert.Equal("jei-v2", update.LatestVersionId);
    }

    [Fact]
    public async Task Ignora_arquivos_que_nao_sao_do_modrinth()
    {
        var version = Fakes.NewDraftVersion();
        version.UpsertFile(new ModpackFile
        {
            ModpackVersionId = version.Id,
            ProjectSlug = "algum-cf-mod",
            Path = "mods/cf.jar",
            Sha256 = new string('b', 64),
            SizeBytes = 10,
            Side = FileSide.Both,
            Origin = ModFileOrigin.CurseForge, // não é checado
            OriginReference = "antigo"
        });

        var repo = new FakeModpackRepository();
        repo.Seed(version);
        var resolver = new FakeResolver(new Dictionary<string, string> { ["algum-cf-mod"] = "novo" });

        var useCase = new CheckModpackVersionUpdates(repo, [resolver]);
        var result = await useCase.HandleAsync(version.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task Falha_quando_a_versao_nao_existe()
    {
        var repo = new FakeModpackRepository(); // nada semeado
        var useCase = new CheckModpackVersionUpdates(repo, [new FakeResolver(new Dictionary<string, string>())]);

        var result = await useCase.HandleAsync(Guid.CreateVersion7(), CancellationToken.None);

        Assert.False(result.Succeeded);
    }
}

public sealed class CloneVersionTests
{
    [Fact]
    public async Task Clona_os_arquivos_num_draft_novo()
    {
        var source = Fakes.NewDraftVersion();
        source.UpsertFile(Fakes.ModrinthFile(source, "jei", "jei-v1"));
        source.UpsertFile(Fakes.ModrinthFile(source, "sodium", "sodium-v1"));

        var repo = new FakeModpackRepository();
        repo.Seed(source);
        var useCase = new CloneVersion(repo);

        var result = await useCase.HandleAsync(source.Id, "2.0", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(repo.Added);

        var clone = repo.Added!;
        Assert.NotEqual(source.Id, clone.Id); // é uma versão nova
        Assert.Equal("2.0", clone.Version);
        Assert.Equal(ModpackVersionState.Draft, clone.State); // nasce editável
        Assert.Equal(2, clone.Files.Count);
        Assert.Equal(clone.Id, result.Value);

        // Arquivos copiados preservando identidade e referência fixada.
        var jei = clone.Files.Single(f => f.ProjectSlug == "jei");
        Assert.Equal("jei-v1", jei.OriginReference);
        Assert.Equal(source.Files.Single(f => f.ProjectSlug == "jei").Sha256, jei.Sha256);
    }

    [Fact]
    public async Task Falha_com_numero_de_versao_vazio()
    {
        var source = Fakes.NewDraftVersion();
        var repo = new FakeModpackRepository();
        repo.Seed(source);
        var useCase = new CloneVersion(repo);

        var result = await useCase.HandleAsync(source.Id, "   ", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(repo.Added);
    }

    [Fact]
    public async Task Falha_quando_a_origem_nao_existe()
    {
        var repo = new FakeModpackRepository();
        var useCase = new CloneVersion(repo);

        var result = await useCase.HandleAsync(Guid.CreateVersion7(), "2.0", CancellationToken.None);

        Assert.False(result.Succeeded);
    }
}

// ---- Fakes e fábricas partilhados neste arquivo ----

internal static class Fakes
{
    public static ModpackVersion NewDraftVersion() =>
        new() { ModpackId = Guid.CreateVersion7(), Version = "1.0", LoaderVersion = "21.1.234" };

    public static ModpackFile ModrinthFile(ModpackVersion version, string slug, string pinnedVersionId)
    {
        return new ModpackFile
        {
            ModpackVersionId = version.Id,
            ProjectSlug = slug,
            Path = $"mods/{slug}.jar",
            Sha256 = new string('a', 64),
            SizeBytes = 10,
            Side = FileSide.Both,
            Origin = ModFileOrigin.Modrinth,
            OriginReference = pinnedVersionId // id da versão fixada
        };
    }
}

// Resolver configurável: mapeia projectId → id da versão "mais recente".
internal sealed class FakeResolver(Dictionary<string, string> latestVersionIds) : IModResolver
{
    public ModFileOrigin Origin => ModFileOrigin.Modrinth;
    public ValueTask<bool> IsAvailableAsync(CancellationToken ct) => ValueTask.FromResult(true);

    public Task<ModResolution> ResolveAsync(ModRequest request, CancellationToken ct)
    {
        if (!latestVersionIds.TryGetValue(request.ProjectId, out var versionId))
            return Task.FromResult<ModResolution>(new ModResolution.NotFound("não encontrado"));

        var resolved = new ModResolution.Resolved(
            versionId,
            $"{request.ProjectId}-{versionId}.jar",
            null,
            10,
            new Uri($"https://example.test/{request.ProjectId}.jar"),
            []);

        return Task.FromResult<ModResolution>(resolved);
    }
}

internal sealed class FakeModpackRepository : IModpackRepository
{
    private readonly Dictionary<Guid, ModpackVersion> _versions = new();

    public ModpackVersion? Added { get; private set; }

    public Task<ModpackVersion?> GetVersionAsync(Guid versionId, CancellationToken ct) =>
        Task.FromResult(_versions.GetValueOrDefault(versionId));

    public Task AddVersionAsync(ModpackVersion version, CancellationToken ct)
    {
        Added = version;
        _versions[version.Id] = version;
        return Task.CompletedTask;
    }

    public Task RemoveVersionAsync(Guid versionId, CancellationToken ct) => throw new NotImplementedException();

    public Task UpdateVersionAsync(ModpackVersion version, CancellationToken ct) => Task.CompletedTask;

    public Task<Modpack?> GetWithVersionsAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();

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

    public void Seed(ModpackVersion version) => _versions[version.Id] = version;
}
