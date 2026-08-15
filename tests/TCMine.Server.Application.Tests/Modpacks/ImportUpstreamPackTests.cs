using System.Text;
using TCMine.Contracts.Modpacks;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Domain.Modpacks;

using TCMine.Server.Application.Tests.Fakes;

namespace TCMine.Server.Application.Tests.Modpacks;

public sealed class ImportUpstreamPackTests
{
    [Fact]
    public async Task Importa_pack_criando_modpack_e_versao_em_rascunho()
    {
        var repo = new FakeRepo();
        var useCase = Build(repo, PackComDoisModsEUmOverride());

        var result = await useCase.HandleAsync(ModFileOrigin.CurseForge, "999", null, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(repo.Created);
        Assert.Equal("1.21.1", repo.Created!.MinecraftVersion);
        Assert.Equal(ModLoader.NeoForge, repo.Created.Loader);

        // A procedência fica gravada: é por ela que se detecta atualização depois.
        Assert.Equal(ModFileOrigin.CurseForge, repo.Created.UpstreamProvider);
        Assert.Equal("999", repo.Created.UpstreamProjectId);

        // Versão nasce em rascunho, com a release do CurseForge à parte da nossa.
        Assert.NotNull(repo.AddedVersion);
        Assert.Equal(ModpackVersionState.Draft, repo.AddedVersion!.State);
        Assert.Equal("1.0.0", repo.AddedVersion.Version);
        Assert.Equal("5555", repo.AddedVersion.UpstreamFileId);
        Assert.Equal("4.2", repo.AddedVersion.UpstreamVersionLabel);
    }

    [Fact]
    public async Task Grava_overrides_e_enfileira_os_mods()
    {
        var repo = new FakeRepo();
        var queue = new FakeQueue();
        var useCase = Build(repo, PackComDoisModsEUmOverride(), queue);

        await useCase.HandleAsync(ModFileOrigin.CurseForge, "999", null, CancellationToken.None);

        // Override entra resolvido (o conteúdo veio no zip)...
        var arquivo = Assert.Single(repo.AddedVersion!.Files);
        Assert.Equal("config/mod.toml", arquivo.Path);
        Assert.Equal(ModFileOrigin.Override, arquivo.Origin);

        // ...e os mods vão para a fila, que baixa em background.
        Assert.Equal(2, queue.Enfileirados.Count);
        Assert.All(queue.Enfileirados, i => Assert.Equal(ModFileOrigin.CurseForge, i.Origin));
    }

    [Fact]
    public async Task Snapshot_registra_mods_e_overrides_como_base_do_merge()
    {
        var repo = new FakeRepo();
        var useCase = Build(repo, PackComDoisModsEUmOverride());

        await useCase.HandleAsync(ModFileOrigin.CurseForge, "999", null, CancellationToken.None);

        var snapshot = UpstreamSnapshot.FromJson(repo.AddedVersion!.UpstreamSnapshotJson);
        Assert.NotNull(snapshot);

        // Sem esta base não dá para saber, na atualização, se um mod mudou
        // porque o autor mexeu ou porque o admin mexeu.
        Assert.Equal(2, snapshot!.Mods.Count);
        Assert.Equal("111", snapshot.Mods["1"]);
        Assert.Equal("222", snapshot.Mods["2"]);
        Assert.Single(snapshot.Overrides);
        Assert.True(snapshot.Overrides.ContainsKey("config/mod.toml"));
    }

    [Fact]
    public async Task Traz_a_capa_do_pack_junto()
    {
        var repo = new FakeRepo();
        var useCase = Build(repo, PackComDoisModsEUmOverride() with { IconUrl = "https://cdn.test/capa.png" });

        await useCase.HandleAsync(ModFileOrigin.CurseForge, "999", null, CancellationToken.None);

        // Sem isto, um catálogo importado vira um mural de cards cinzentos e o
        // admin teria de subir cada capa à mão.
        Assert.NotNull(repo.Created!.IconBlobSha256);
    }

    [Fact]
    public async Task Capa_indisponivel_nao_derruba_a_importacao()
    {
        var repo = new FakeRepo();
        var useCase = Build(repo, PackComDoisModsEUmOverride()); // sem IconUrl

        var result = await useCase.HandleAsync(ModFileOrigin.CurseForge, "999", null, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(repo.Created!.IconBlobSha256);
    }

    [Fact]
    public async Task Recusa_importar_o_mesmo_pack_duas_vezes()
    {
        var repo = new FakeRepo { JaImportado = true };
        var useCase = Build(repo, PackComDoisModsEUmOverride());

        var result = await useCase.HandleAsync(ModFileOrigin.CurseForge, "999", null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(repo.Created);
    }

    [Fact]
    public async Task Falha_quando_a_origem_nao_esta_configurada()
    {
        var repo = new FakeRepo();
        var source = new FakeSource(PackComDoisModsEUmOverride()) { Disponivel = false };
        var useCase = new ImportUpstreamPack(
            [source], repo, new FakeBlobStore(), new IngestionScheduler(repo, new FakeQueue()), new FakeJobProgress(),
            new FakeDownloader(), new FakeScope());

        var result = await useCase.HandleAsync(ModFileOrigin.CurseForge, "999", null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(repo.Created);
    }

    // ---- Fixtures ----

    private static ImportUpstreamPack Build(FakeRepo repo, UpstreamPack pack, FakeQueue? queue = null) =>
        new([new FakeSource(pack)], repo, new FakeBlobStore(),
            new IngestionScheduler(repo, queue ?? new FakeQueue()),
            new FakeJobProgress(), new FakeDownloader(), new FakeScope());

    private static UpstreamPack PackComDoisModsEUmOverride() => new()
    {
        ProjectId = "999",
        FileId = "5555",
        VersionLabel = "4.2",
        Name = "Pack de Teste",
        MinecraftVersion = "1.21.1",
        Loader = ModLoader.NeoForge,
        LoaderVersion = "21.1.100",
        Mods = [new UpstreamPackMod("1", "111", true), new UpstreamPackMod("2", "222", true)],
        Overrides = [new UpstreamPackOverride("config/mod.toml", Encoding.UTF8.GetBytes("a=1"))]
    };

    // ---- Fakes ----

    private sealed class FakeSource(UpstreamPack pack) : FakeUpstreamPackSourceBase
    {
        public bool Disponivel { get; init; } = true;
        public override ModFileOrigin Origin => ModFileOrigin.CurseForge;
        public override ValueTask<bool> IsAvailableAsync(CancellationToken ct) => ValueTask.FromResult(Disponivel);

        public override Task<UpstreamPack?> FetchAsync(string projectId, string? fileId, CancellationToken ct) =>
            Task.FromResult<UpstreamPack?>(pack);

    }

    private sealed class FakeBlobStore : FakeBlobStoreBase
    {
        public override Task<string> PutAsync(Stream content, string? expectedSha256, string contentType, CancellationToken ct)
        {
            // Hash fictício mas estável: o teste só precisa que exista um.
            return Task.FromResult(new string('b', 64));
        }

        public override Task<bool> ExistsAsync(string sha256, CancellationToken ct) => Task.FromResult(true);
        public override Task<Uri?> TryGetDirectUrlAsync(string sha256, TimeSpan lifetime, CancellationToken ct) =>
            Task.FromResult<Uri?>(null);

        public override Task<string?> TryGetLocalPathAsync(string sha256, CancellationToken ct) =>
            Task.FromResult<string?>(null);
    }

    private sealed class FakeDownloader : IModDownloader
    {
        public Task<Stream> OpenAsync(Uri url, CancellationToken ct) =>
            Task.FromResult<Stream>(new MemoryStream([1, 2, 3, 4]));
    }

    private sealed class FakeQueue : IIngestionQueue
    {
        public List<ModIngestionItem> Enfileirados { get; } = [];

        public ValueTask EnqueueAsync(Guid versionId, IReadOnlyList<ModIngestionItem> items, CancellationToken ct)
        {
            Enfileirados.AddRange(items);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeScope : ICurrentUserScope
    {
        public Guid? UserId => Guid.Empty;
        public Guid OwnerId => Guid.Empty;
        public bool IsInstanceAdmin => true;

        public Task<ServerRoleDto?> GetRoleAsync(Guid gameServerId, CancellationToken ct) =>
            Task.FromResult<ServerRoleDto?>(ServerRoleDto.Owner);
    }

    private sealed class FakeRepo : FakeModpackRepositoryBase
    {
        public bool JaImportado { get; init; }
        public Modpack? Created { get; private set; }
        public ModpackVersion? AddedVersion { get; private set; }

        public override Task<bool> ExistsFromUpstreamAsync(ModFileOrigin origin, string projectId, CancellationToken ct) =>
            Task.FromResult(JaImportado);

        public override Task<bool> SlugExistsAsync(string slug, CancellationToken ct) => Task.FromResult(false);

        public override Task CreateAsync(Modpack modpack, CancellationToken ct)
        {
            Created = modpack;
            return Task.CompletedTask;
        }

        public override Task AddVersionAsync(ModpackVersion version, CancellationToken ct)
        {
            AddedVersion = version;
            return Task.CompletedTask;
        }

    }
}
