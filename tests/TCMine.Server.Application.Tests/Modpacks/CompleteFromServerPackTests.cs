using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

/// <summary>
///     Completar um rascunho com o server pack do autor.
///     O caso existe porque no CurseForge o autor pode proibir que terceiros
///     baixem o .jar pela API — é a causa da maioria das pendências — e ao mesmo
///     tempo publicar um server pack que traz esses mesmos arquivos DENTRO do
///     zip. O que a API nega em separado, o autor entrega junto.
/// </summary>
public sealed class CompleteFromServerPackTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Preenche_a_pendencia_e_a_remove()
    {
        var version = Rascunho();
        version.UpsertPending(Pendencia(version.Id, "1234", "5678", "Corail Tombstone"));

        var (caso, repo) = Montar(version, new FakePack(("tombstone-1.21.jar", "conteudo")),
            new Dictionary<string, string> { ["5678"] = "tombstone-1.21.jar" });

        var result = await caso.HandleAsync(version.Id, Ct);

        result.Succeeded.ShouldBeTrue(result.Error);
        result.Value!.Filled.ShouldBe(1);
        result.Value.Remaining.ShouldBe(0);

        version.ManualUploads.ShouldBeEmpty("a pendência foi atendida");
        version.Files.Single().Path.ShouldBe("mods/tombstone-1.21.jar");
        repo.PendenciasRemovidas.ShouldBe(1);
    }

    [Fact]
    public async Task Mantem_a_identidade_do_mod_no_arquivo_gravado()
    {
        // O ProjectSlug é o que faz uma atualização futura SUBSTITUIR este .jar
        // em vez de acrescentar um segundo. Dois .jar do mesmo mod na pasta
        // mods/ derrubam o jogo no arranque.
        var version = Rascunho();
        version.UpsertPending(Pendencia(version.Id, "1234", "5678", "Tombstone", FileSide.ClientOnly));

        var (caso, _) = Montar(version, new FakePack(("tombstone-1.21.jar", "x")),
            new Dictionary<string, string> { ["5678"] = "tombstone-1.21.jar" });

        await caso.HandleAsync(version.Id, Ct);

        var arquivo = version.Files.Single();
        arquivo.ProjectSlug.ShouldBe("1234");
        arquivo.OriginReference.ShouldBe("5678");
        arquivo.Side.ShouldBe(FileSide.ClientOnly, "o lado vem da pendência, não é chutado como Both");
    }

    [Fact]
    public async Task Pendencia_que_nao_esta_no_server_pack_continua_pendente()
    {
        var version = Rascunho();
        version.UpsertPending(Pendencia(version.Id, "1234", "5678", "Tombstone"));
        version.UpsertPending(Pendencia(version.Id, "9999", "8888", "Mod só de cliente"));

        var (caso, _) = Montar(version, new FakePack(("tombstone-1.21.jar", "x")),
            new Dictionary<string, string> { ["5678"] = "tombstone-1.21.jar", ["8888"] = "so-cliente.jar" });

        var result = await caso.HandleAsync(version.Id, Ct);

        result.Value!.Filled.ShouldBe(1);
        result.Value.Remaining.ShouldBe(1);
        version.ManualUploads.Single().ProjectSlug.ShouldBe("9999");
    }

    [Fact]
    public async Task Recusa_versao_publicada()
    {
        // Mesma regra de toda edição: versão publicada é imutável, e acrescentar
        // arquivos nela mudaria o que já foi prometido a quem instalou.
        var version = Rascunho();
        version.UpsertPending(Pendencia(version.Id, "1234", "5678", "Tombstone"));
        version.UpsertFile(Arquivo(version.Id));
        version.MarkResolving();
        version.MarkReady();

        var (caso, _) = Montar(version, new FakePack(("tombstone-1.21.jar", "x")),
            new Dictionary<string, string> { ["5678"] = "tombstone-1.21.jar" });

        var result = await caso.HandleAsync(version.Id, Ct);

        result.Succeeded.ShouldBeFalse();
        result.Error!.ShouldContain("rascunho");
    }

    [Fact]
    public async Task Diz_quando_o_autor_bloqueou_tambem_o_server_pack()
    {
        var version = Rascunho();
        version.UpsertPending(Pendencia(version.Id, "1234", "5678", "Tombstone"));

        var (caso, _) = Montar(version, null,
            new Dictionary<string, string> { ["5678"] = "tombstone-1.21.jar" });

        var result = await caso.HandleAsync(version.Id, Ct);

        result.Succeeded.ShouldBeFalse();
        result.Error!.ShouldContain("bloqueou");
    }

    private static (CompleteFromServerPack Caso, FakeRepo Repo) Montar(
        ModpackVersion version, FakePack? pack, Dictionary<string, string> fileNames)
    {
        var origem = new FakeSource { ServerPack = pack };
        foreach (var (id, nome) in fileNames)
            origem.FileNames[id] = nome;

        var repo = new FakeRepo(version);

        return (new CompleteFromServerPack(
            [origem], repo, new FakeBlobs(), new FakeJobProgress(),
            NullLogger<CompleteFromServerPack>.Instance), repo);
    }

    private static ModpackVersion Rascunho() => new()
    {
        ModpackId = Guid.CreateVersion7(),
        Version = "1.0.0",
        LoaderVersion = "21.1.100",
        UpstreamServerPackFileId = "777"
    };

    private static PendingMod Pendencia(
        Guid versionId, string slug, string fileId, string nome, FileSide side = FileSide.Both) =>
        new()
        {
            ModpackVersionId = versionId,
            ProjectSlug = slug,
            DisplayName = nome,
            Origin = ModFileOrigin.CurseForge,
            FileId = fileId,
            Side = side,
            Reason = PendingModReason.DistributionDenied
        };

    private static ModpackFile Arquivo(Guid versionId) => new()
    {
        ModpackVersionId = versionId,
        Path = "mods/ja-tinha.jar",
        Sha256 = new string('a', 64),
        SizeBytes = 10,
        Side = FileSide.Both,
        Origin = ModFileOrigin.CurseForge,
        ProjectSlug = "ja-tinha"
    };

    private sealed class FakeSource : FakeUpstreamPackSourceBase;

    private sealed class FakePack(params (string Nome, string Conteudo)[] mods) : IServerPackReader
    {
        public IReadOnlyCollection<string> ModFileNames => [.. mods.Select(m => m.Nome)];

        public Stream OpenMod(string fileName) =>
            new MemoryStream(Encoding.UTF8.GetBytes(mods.First(m => m.Nome == fileName).Conteudo));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeBlobs : FakeBlobStoreBase
    {
        private readonly Dictionary<string, byte[]> _conteudo = [];

        public override async Task<string> PutAsync(
            Stream content, string? expectedSha256, string contentType, CancellationToken ct)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct);

            var bytes = buffer.ToArray();
            var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
            _conteudo[sha] = bytes;
            return sha;
        }

        public override Task<Stream> OpenAsync(string sha256, CancellationToken ct) =>
            Task.FromResult<Stream>(new MemoryStream(_conteudo[sha256]));
    }

    private sealed class FakeRepo(ModpackVersion version) : FakeModpackRepositoryBase
    {
        public int PendenciasRemovidas { get; private set; }

        public override Task<ModpackVersion?> GetVersionAsync(Guid versionId, CancellationToken ct) =>
            Task.FromResult<ModpackVersion?>(version);

        public override Task<Modpack?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<Modpack?>(new Modpack
            {
                Slug = "atm10",
                Name = "All the Mods 10",
                MinecraftVersion = "1.21.1",
                Loader = ModLoader.NeoForge,
                UpstreamProvider = ModFileOrigin.CurseForge,
                UpstreamProjectId = "925200"
            });

        public override Task UpdateVersionAsync(ModpackVersion v, CancellationToken ct) => Task.CompletedTask;

        public override Task RemovePendingAsync(Guid versionId, Guid pendingId, CancellationToken ct)
        {
            PendenciasRemovidas++;
            return Task.CompletedTask;
        }
    }
}
