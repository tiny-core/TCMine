using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

/// <summary>
///     Preenchimento retroativo do server pack.
///     Existe porque a saída pelo server pack chegou depois dos packs já
///     importados — e quem tem um pack grande, com uma dúzia de pendências, é
///     justamente quem precisa dela. Sem isto, ganhar o botão exigiria
///     reimportar tudo.
/// </summary>
public sealed class BackfillServerPacksTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Preenche_versao_importada_que_ainda_nao_sabia()
    {
        var versao = Versao(upstreamFileId: "5555");
        var repo = new FakeRepo(Modpack(), versao);
        var origem = new FakeSource { ServerPackInfo = new UpstreamServerPack("777", "https://exemplo/pack") };

        var total = await new BackfillServerPacks([origem], repo).HandleAsync(Ct);

        total.ShouldBe(1);
        repo.ServerPacksGravados[versao.Id].ShouldBe(("777", "https://exemplo/pack"));
    }

    [Fact]
    public async Task Nao_reconsulta_o_que_ja_sabe()
    {
        // A condição é o que limita o trabalho a uma vez por versão: uma vez
        // preenchida, ela deixa de ser candidata e o arranque seguinte não gasta
        // chamada nenhuma com ela.
        var versao = Versao(upstreamFileId: "5555");
        versao.UpstreamServerPackFileId = "ja-sabia";

        var repo = new FakeRepo(Modpack(), versao);
        var origem = new FakeSource { ServerPackInfo = new UpstreamServerPack("777", null) };

        var total = await new BackfillServerPacks([origem], repo).HandleAsync(Ct);

        total.ShouldBe(0);
        repo.ServerPacksGravados.ShouldBeEmpty();
    }

    [Fact]
    public async Task Ignora_versao_que_nao_veio_de_origem_externa()
    {
        // Versão criada à mão não tem release na origem para consultar.
        var repo = new FakeRepo(Modpack(), Versao(upstreamFileId: null));
        var origem = new FakeSource { ServerPackInfo = new UpstreamServerPack("777", null) };

        var total = await new BackfillServerPacks([origem], repo).HandleAsync(Ct);

        total.ShouldBe(0);
    }

    [Fact]
    public async Task Nao_grava_nada_quando_a_release_nao_tem_server_pack()
    {
        var repo = new FakeRepo(Modpack(), Versao(upstreamFileId: "5555"));
        var origem = new FakeSource { ServerPackInfo = null };

        var total = await new BackfillServerPacks([origem], repo).HandleAsync(Ct);

        total.ShouldBe(0);
        repo.ServerPacksGravados.ShouldBeEmpty();
    }

    [Fact]
    public async Task Sem_origem_disponivel_nao_falha_nem_grava()
    {
        // Sem chave de API a origem se declara indisponível. Isso não é erro: a
        // informação não é urgente e o próximo arranque tenta de novo.
        var repo = new FakeRepo(Modpack(), Versao(upstreamFileId: "5555"));
        var origem = new FakeSource { Disponivel = false };

        var total = await new BackfillServerPacks([origem], repo).HandleAsync(Ct);

        total.ShouldBe(0);
        repo.ServerPacksGravados.ShouldBeEmpty();
    }

    private static Modpack Modpack() => new()
    {
        Slug = "atm10",
        Name = "All the Mods 10",
        MinecraftVersion = "1.21.1",
        Loader = ModLoader.NeoForge,
        UpstreamProvider = ModFileOrigin.CurseForge,
        UpstreamProjectId = "925200"
    };

    private static ModpackVersion Versao(string? upstreamFileId) => new()
    {
        ModpackId = Guid.CreateVersion7(),
        Version = "1.0.0",
        LoaderVersion = "21.1.100",
        UpstreamFileId = upstreamFileId
    };

    private sealed class FakeSource : FakeUpstreamPackSourceBase
    {
        public bool Disponivel { get; init; } = true;

        public override ValueTask<bool> IsAvailableAsync(CancellationToken ct) =>
            ValueTask.FromResult(Disponivel);
    }

    private sealed class FakeRepo(Modpack modpack, ModpackVersion versao) : FakeModpackRepositoryBase
    {
        public override Task<IReadOnlyList<Modpack>> ListAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Modpack>>([modpack]);

        public override Task<IReadOnlyList<ModpackVersion>> ListVersionsAsync(
            Guid modpackId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ModpackVersion>>([versao]);
    }
}
