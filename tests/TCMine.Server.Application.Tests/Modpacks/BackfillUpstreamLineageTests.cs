using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

/// <summary>
///     As versões criadas antes de a ligação com o autor ser copiada a recebem
///     de volta no arranque — mas só as que de fato vieram da importada.
/// </summary>
public sealed class BackfillUpstreamLineageTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string Snapshot = """{"mods":{"jei":"1","create":"2","mekanism":"3","ae2":"4"},"overrides":{}}""";

    [Fact]
    public async Task Versao_herdada_e_podada_recupera_a_ligacao()
    {
        var (modpack, importada) = Importado();
        var propria = Versao(modpack, "jei", "create", "meu-mod"); // tirou dois, pôs um
        var repo = new Repo(modpack, importada, propria);

        var count = await new BackfillUpstreamLineage(repo).HandleAsync(Ct);

        count.ShouldBe(1);
        repo.LinhagensGravadas[propria.Id].ShouldBe(importada.Id);
    }

    [Fact]
    public async Task Versao_criada_vazia_e_preenchida_a_mao_nao_e_ligada()
    {
        // Ligar esta ao autor faria o merge recusar as atualizações dele.
        var (modpack, importada) = Importado();
        var outra = Versao(modpack, "meu-mod", "outro-mod");
        var repo = new Repo(modpack, importada, outra);

        (await new BackfillUpstreamLineage(repo).HandleAsync(Ct)).ShouldBe(0);
    }

    [Fact]
    public async Task A_ligacao_anda_de_versao_em_versao()
    {
        // Importada → 1.1 (sem ligação) → 1.2 (sem ligação): as duas recebem.
        var (modpack, importada) = Importado();
        var v11 = Versao(modpack, "jei", "create", "mekanism");
        var v12 = Versao(modpack, "jei", "create", "mekanism", "ae2");
        var repo = new Repo(modpack, importada, v11, v12);

        (await new BackfillUpstreamLineage(repo).HandleAsync(Ct)).ShouldBe(2);
        repo.LinhagensGravadas[v12.Id].ShouldBe(v11.Id);
    }

    private static (Modpack, ModpackVersion) Importado()
    {
        var modpack = new Modpack
        {
            Name = "ATM", Slug = "atm", MinecraftVersion = "1.21.1", Loader = ModLoader.NeoForge,
            UpstreamProvider = ModFileOrigin.CurseForge, UpstreamProjectId = "925200"
        };

        var importada = Versao(modpack, "jei", "create", "mekanism", "ae2");
        importada.UpstreamFileId = "555";
        importada.UpstreamSnapshotJson = Snapshot;
        return (modpack, importada);
    }

    /// <remarks>
    ///     A ordem das versões é a do Id (GUID v7), e dois Ids gerados no mesmo
    ///     milissegundo não têm ordem garantida — o teste em cadeia passava ou
    ///     falhava conforme o relógio. A pausa põe cada versão no seu próprio
    ///     milissegundo, como acontece de verdade entre duas versões criadas.
    /// </remarks>
    private static ModpackVersion Versao(Modpack modpack, params string[] slugs)
    {
        Thread.Sleep(2);

        var version = new ModpackVersion { ModpackId = modpack.Id, Version = Guid.NewGuid().ToString("N")[..6], LoaderVersion = "21.1.100" };
        foreach (var slug in slugs)
        {
            version.UpsertFile(new ModpackFile
            {
                ModpackVersionId = version.Id,
                Path = $"mods/{slug}.jar",
                Sha256 = new string('a', 64),
                SizeBytes = 1,
                Side = FileSide.Both,
                Origin = ModFileOrigin.CurseForge,
                ProjectSlug = slug
            });
        }

        return version;
    }

    private sealed class Repo(Modpack modpack, params ModpackVersion[] versions) : FakeModpackRepositoryBase
    {
        public override Task<IReadOnlyList<Modpack>> ListAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Modpack>>([modpack]);

        public override Task<IReadOnlyList<ModpackVersion>> ListVersionsAsync(Guid modpackId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ModpackVersion>>(versions);
    }
}
