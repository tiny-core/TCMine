using System.Text;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

/// <summary>
///     "Só em rascunho" é a regra que sustenta a imutabilidade — a promessa de
///     que um pack que funcionava continua funcionando daqui a um ano. Ela está
///     repetida em vários casos de uso, e cada um pode perdê-la sozinho: por isso
///     um teste para cada.
/// </summary>
public sealed class DraftOnlyGuardTests
{
    [Fact]
    public async Task Nao_edita_metadados_de_versao_publicada()
    {
        var version = Publicada();

        var result = await new UpdateModpackVersion(new FakeRepo(version))
            .HandleAsync(version.Id, "9.9.9", "21.1.999", 8192, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("1.0.0", version.Version);
    }

    [Fact]
    public async Task Edita_metadados_de_rascunho()
    {
        var version = Rascunho();

        var result = await new UpdateModpackVersion(new FakeRepo(version))
            .HandleAsync(version.Id, "  1.2.0  ", "  21.1.247  ", 8192, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("1.2.0", version.Version);
        Assert.Equal("21.1.247", version.LoaderVersion);
        Assert.Equal(8192, version.RecommendedMemoryMb);
    }

    [Fact]
    public async Task Nao_edita_versao_do_loader_de_versao_publicada()
    {
        // A versão do loader sobe entre versões, mas dentro de UMA versão ela é
        // parte do que foi publicado: mudá-la depois faria quem já instalou
        // rodar contra um loader diferente do que o manifesto prometeu.
        var version = Publicada();

        var result = await new UpdateModpackVersion(new FakeRepo(version))
            .HandleAsync(version.Id, "1.0.0", "21.1.999", null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("21.1.100", version.LoaderVersion);
    }

    [Fact]
    public async Task Nao_aceita_versao_do_loader_em_branco()
    {
        // É ela que instala o loader no cliente e no container do servidor. Em
        // branco, a instância não sobe — e o erro apareceria só na hora de jogar.
        var version = Rascunho();

        var result = await new UpdateModpackVersion(new FakeRepo(version))
            .HandleAsync(version.Id, "1.2.0", "   ", null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("21.1.100", version.LoaderVersion);
        Assert.Equal("1.0.0", version.Version);
    }

    [Fact]
    public async Task Nao_remove_arquivo_de_versao_publicada()
    {
        var version = Publicada();
        var repo = new FakeRepo(version);

        var result = await new RemoveModpackFile(repo)
            .HandleAsync(version.Id, version.Files[0].Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(repo.ArquivoRemovido);
    }

    [Fact]
    public async Task Remove_arquivo_de_rascunho_mantendo_o_blob()
    {
        var version = Rascunho();
        version.UpsertFile(Arquivo(version.Id, "mods/jei.jar", "jei"));
        var repo = new FakeRepo(version);

        var result = await new RemoveModpackFile(repo)
            .HandleAsync(version.Id, version.Files[0].Id, CancellationToken.None);

        Assert.True(result.Succeeded);

        // Só o vínculo sai: o blob pode estar em uso por outra versão, e é o GC
        // de órfãos que decide apagá-lo.
        Assert.True(repo.ArquivoRemovido);
    }

    [Fact]
    public async Task Nao_apaga_override_de_versao_publicada()
    {
        // O override entra ANTES de publicar: o próprio domínio já recusa mexer
        // em arquivos de uma versão Ready, e o cenário aqui é o caso de uso.
        var version = Rascunho();
        version.UpsertFile(Arquivo(version.Id, "config/x.toml", "override:config/x.toml",
            ModFileOrigin.Override));
        version.MarkResolving();
        version.MarkReady();

        var repo = new FakeRepo(version);

        var result = await new DeleteOverride(repo)
            .HandleAsync(version.Id, "config/x.toml", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(repo.ArquivoRemovido);
    }

    [Fact]
    public async Task Nao_salva_override_em_versao_publicada()
    {
        var version = Publicada();

        var result = await new SaveOverride(new FakeRepo(version), new FakeBlobStore())
            .HandleAsync(version.Id, "config/x.toml", "a=1", CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Salva_override_em_rascunho()
    {
        var version = Rascunho();

        var result = await new SaveOverride(new FakeRepo(version), new FakeBlobStore())
            .HandleAsync(version.Id, "config/x.toml", "a=1", CancellationToken.None);

        Assert.True(result.Succeeded);

        var arquivo = Assert.Single(version.Files);
        Assert.Equal("config/x.toml", arquivo.Path);
        Assert.Equal(ModFileOrigin.Override, arquivo.Origin);
    }

    // ---- Fixtures ----

    private static ModpackVersion Rascunho() => new()
    {
        ModpackId = Guid.CreateVersion7(), Version = "1.0.0", LoaderVersion = "21.1.100"
    };

    private static ModpackVersion Publicada()
    {
        var version = Rascunho();
        version.UpsertFile(Arquivo(version.Id, "mods/x.jar", "x"));
        version.MarkResolving();
        version.MarkReady();
        return version;
    }

    private static ModpackFile Arquivo(
        Guid versionId, string path, string slug, ModFileOrigin origin = ModFileOrigin.Modrinth) => new()
    {
        ModpackVersionId = versionId,
        Path = path,
        Sha256 = new string('a', 64),
        SizeBytes = 10,
        Side = FileSide.Both,
        Origin = origin,
        ProjectSlug = slug
    };

    // ---- Fakes ----

    private sealed class FakeBlobStore : FakeBlobStoreBase
    {
        public override Task<string> PutAsync(
            Stream content, string? expectedSha256, string contentType, CancellationToken ct) =>
            Task.FromResult(new string('c', 64));

        public override Task<Stream> OpenAsync(string sha256, CancellationToken ct) =>
            Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("a=1")));
    }

    private sealed class FakeRepo(ModpackVersion version) : FakeModpackRepositoryBase
    {
        public bool ArquivoRemovido { get; private set; }

        public override Task<ModpackVersion?> GetVersionAsync(Guid versionId, CancellationToken ct) =>
            Task.FromResult<ModpackVersion?>(version);

        public override Task UpdateVersionAsync(ModpackVersion v, CancellationToken ct) => Task.CompletedTask;

        public override Task<IReadOnlyList<ModpackVersion>> ListVersionsAsync(
            Guid modpackId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ModpackVersion>>([version]);

        public override Task RemoveFileAsync(Guid versionId, Guid fileId, CancellationToken ct)
        {
            ArquivoRemovido = true;
            return Task.CompletedTask;
        }
    }
}
