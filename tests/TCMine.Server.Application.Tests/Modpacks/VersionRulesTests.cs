using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

/// <summary>
///     Regras de criação e remoção de versão. Todas existem para o mesmo fim:
///     manter a promessa de que uma versão publicada nunca muda.
/// </summary>
public sealed class VersionRulesTests
{
    [Fact]
    public async Task So_permite_um_rascunho_por_vez()
    {
        // Dois rascunhos em paralelo produzem duas versões meio-feitas e ninguém
        // sabe qual publicar.
        var modpack = NovoModpack();
        var rascunho = Versao(modpack, "1.1.0");
        modpack.Versions.Add(rascunho);

        var result = await new CreateModpackVersion(new FakeRepo(modpack)).HandleAsync(
            new CreateModpackVersionCommand(modpack.Id, "1.2.0", "21.1.100", null, false),
            CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Recusa_numero_de_versao_repetido()
    {
        var modpack = NovoModpack();
        var publicada = Publicar(Versao(modpack, "1.0.0"));
        modpack.Versions.Add(publicada);

        var result = await new CreateModpackVersion(new FakeRepo(modpack)).HandleAsync(
            new CreateModpackVersionCommand(modpack.Id, "  1.0.0  ", "21.1.100", null, false),
            CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Herda_os_arquivos_da_ultima_publicada_apontando_para_os_mesmos_blobs()
    {
        var modpack = NovoModpack();
        var publicada = Versao(modpack, "1.0.0");
        publicada.UpsertFile(Arquivo(publicada.Id, "mods/jei.jar", "jei"));
        Publicar(publicada);
        modpack.Versions.Add(publicada);

        var repo = new FakeRepo(modpack);

        var result = await new CreateModpackVersion(repo).HandleAsync(
            new CreateModpackVersionCommand(modpack.Id, "1.1.0", "21.1.100", null, true),
            CancellationToken.None);

        Assert.True(result.Succeeded);

        // Mesmo hash: o store é endereçado por conteúdo, então herdar não copia
        // um byte sequer.
        var herdado = Assert.Single(repo.Adicionada!.Files);
        Assert.Equal("jei", herdado.ProjectSlug);
        Assert.Equal(publicada.Files[0].Sha256, herdado.Sha256);
    }

    [Fact]
    public async Task Nao_apaga_versao_publicada()
    {
        // Publicada é imutável e pode ter servidor fixado nela: apagá-la
        // deixaria instâncias apontando para o nada.
        var modpack = NovoModpack();
        var publicada = Publicar(Versao(modpack, "1.0.0"));

        var repo = new FakeRepo(modpack, publicada);

        var result = await new DeleteModpackVersion(repo, new OverrideUndoService())
            .HandleAsync(publicada.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(repo.VersaoRemovida);
    }

    [Fact]
    public async Task Apaga_rascunho()
    {
        var modpack = NovoModpack();
        var rascunho = Versao(modpack, "1.1.0");
        var repo = new FakeRepo(modpack, rascunho);

        var result = await new DeleteModpackVersion(repo, new OverrideUndoService())
            .HandleAsync(rascunho.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(repo.VersaoRemovida);
    }

    // ---- Fixtures ----

    private static Modpack NovoModpack() => new()
    {
        Name = "Pack", Slug = "pack", MinecraftVersion = "1.21.1", Loader = ModLoader.NeoForge
    };

    private static ModpackVersion Versao(Modpack modpack, string numero) => new()
    {
        ModpackId = modpack.Id, Version = numero, LoaderVersion = "21.1.100"
    };

    private static ModpackVersion Publicar(ModpackVersion version)
    {
        if (version.Files.Count is 0)
            version.UpsertFile(Arquivo(version.Id, "mods/x.jar", "x"));

        version.MarkResolving();
        version.MarkReady();
        return version;
    }

    private static ModpackFile Arquivo(Guid versionId, string path, string slug) => new()
    {
        ModpackVersionId = versionId,
        Path = path,
        Sha256 = new string('a', 64),
        SizeBytes = 10,
        Side = FileSide.Both,
        Origin = ModFileOrigin.Modrinth,
        ProjectSlug = slug
    };

    // ---- Fakes ----

    private sealed class FakeRepo(Modpack modpack, ModpackVersion? version = null) : FakeModpackRepositoryBase
    {
        public ModpackVersion? Adicionada { get; private set; }
        public bool VersaoRemovida { get; private set; }

        public override Task<Modpack?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<Modpack?>(modpack);

        public override Task<IReadOnlyList<ModpackVersion>> ListVersionsAsync(Guid modpackId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ModpackVersion>>([.. modpack.Versions]);

        public override Task<ModpackVersion?> GetVersionAsync(Guid versionId, CancellationToken ct) =>
            Task.FromResult(version?.Id == versionId
                ? version
                : modpack.Versions.FirstOrDefault(v => v.Id == versionId));

        public override Task AddVersionAsync(ModpackVersion v, CancellationToken ct)
        {
            Adicionada = v;
            return Task.CompletedTask;
        }

        public override Task RemoveVersionAsync(Guid versionId, CancellationToken ct)
        {
            VersaoRemovida = true;
            return Task.CompletedTask;
        }
    }
}
