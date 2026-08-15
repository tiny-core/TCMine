using TCMine.Contracts.Modpacks;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Tests.Modpacks;

public sealed class DeleteAndQueueTests
{
    [Fact]
    public async Task Nao_apaga_modpack_com_servidor_apontando_para_ele()
    {
        // Apagar deixaria o servidor sem pack: o container continuaria de pé
        // servindo arquivos que o painel já não conhece.
        var modpack = NovoModpack();
        var repo = new FakeModpacks(modpack);

        var result = await new DeleteModpack(repo, new FakeServers(Servidor(modpack.Id)))
            .HandleAsync(modpack.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(repo.Removido);
    }

    [Fact]
    public async Task Apaga_modpack_sem_servidores()
    {
        var modpack = NovoModpack();
        var repo = new FakeModpacks(modpack);

        var result = await new DeleteModpack(repo, new FakeServers()).HandleAsync(
            modpack.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(repo.Removido);
    }

    [Fact]
    public async Task So_enfileira_ingestao_em_rascunho()
    {
        // Enfileirar numa publicada tentaria mexer numa versão imutável, e a
        // ingestão morreria lá atrás sem ninguém ver.
        var version = Rascunho();
        version.UpsertFile(Arquivo(version.Id));
        version.MarkResolving();
        version.MarkReady();

        var queue = new FakeQueue();

        var result = await new QueueIngestion(new FakeModpacks(version), queue).HandleAsync(
            new QueueIngestionCommand(version.Id, [Item("jei")]), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Empty(queue.Enfileirados);
    }

    [Fact]
    public async Task Recusa_ingestao_sem_itens()
    {
        var version = Rascunho();
        var queue = new FakeQueue();

        var result = await new QueueIngestion(new FakeModpacks(version), queue).HandleAsync(
            new QueueIngestionCommand(version.Id, []), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Empty(queue.Enfileirados);
    }

    [Fact]
    public async Task Enfileira_e_volta_na_hora()
    {
        // Retornar assim que enfileira é o contrato: o trabalho pesado roda no
        // worker, e a tela acompanha pelo estado da versão.
        var version = Rascunho();
        var queue = new FakeQueue();

        var result = await new QueueIngestion(new FakeModpacks(version), queue).HandleAsync(
            new QueueIngestionCommand(version.Id, [Item("jei"), Item("create")]), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(2, queue.Enfileirados.Count);
    }

    // ---- Fixtures ----

    private static Modpack NovoModpack() => new()
    {
        Name = "Pack", Slug = "pack", MinecraftVersion = "1.21.1", Loader = ModLoader.NeoForge
    };

    private static ModpackVersion Rascunho() => new()
    {
        ModpackId = Guid.CreateVersion7(), Version = "1.0.0", LoaderVersion = "21.1.100"
    };

    private static ModpackFile Arquivo(Guid versionId) => new()
    {
        ModpackVersionId = versionId,
        Path = "mods/x.jar",
        Sha256 = new string('a', 64),
        SizeBytes = 1,
        Side = FileSide.Both,
        Origin = ModFileOrigin.Modrinth,
        ProjectSlug = "x"
    };

    private static ModIngestionItem Item(string slug) =>
        new(ModFileOrigin.Modrinth, slug, null, FileSide.Both);

    private static GameServer Servidor(Guid modpackId) => new()
    {
        Name = "Servidor",
        ModpackId = modpackId,
        ModpackVersionId = Guid.CreateVersion7(),
        ConnectAddress = "jogo:25565",
        RconSecret = "segredo"
    };

    // ---- Fakes ----

    private sealed class FakeModpacks : FakeModpackRepositoryBase
    {
        private readonly Modpack? _modpack;
        private readonly ModpackVersion? _version;

        public FakeModpacks(Modpack modpack) => _modpack = modpack;
        public FakeModpacks(ModpackVersion version) => _version = version;

        public bool Removido { get; private set; }

        public override Task<Modpack?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(_modpack);

        public override Task<ModpackVersion?> GetVersionAsync(Guid versionId, CancellationToken ct) =>
            Task.FromResult(_version);

        public override Task RemoveAsync(Guid id, CancellationToken ct)
        {
            Removido = true;
            return Task.CompletedTask;
        }
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

    private sealed class FakeServers(params GameServer[] seed) : FakeServerRepositoryBase
    {
        public override Task<IReadOnlyList<GameServer>> ListByModpackAsync(Guid modpackId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<GameServer>>([.. seed]);

    }
}
