using TCMine.Contracts;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

/// <summary>
///     Recuperação das ingestões interrompidas no arranque.
///     A fila vive em memória: um deploy no meio de um pack matava o job, e o
///     admin precisava perceber sozinho e clicar "Tentar novamente". Estes testes
///     travam o comportamento novo — e, principalmente, o freio: recuperação
///     automática sem limite transforma um job que derruba o processo num ciclo
///     de queda no arranque.
/// </summary>
public sealed class RecoverInterruptedIngestionsTests
{
    [Fact]
    public async Task Versao_presa_em_resolving_volta_para_a_fila()
    {
        var (repo, queue, version) = Cenario(naFila: ["3", "4"]);
        version.MarkResolving();

        var retomadas = await Executar(repo, queue);

        retomadas.ShouldBe(1);
        queue.Enfileirados.Select(i => i.ProjectId).Order().ShouldBe(["3", "4"]);

        // Draft, e não Resolving: o worker chama MarkResolving no começo, e essa
        // transição não sai de Resolving. Deixar como estava faria o job ser
        // descartado em silêncio pelo próprio serviço de ingestão.
        version.State.ShouldBe(ModpackVersionState.Draft);
        version.RecoveryAttempts.ShouldBe(1);
    }

    [Fact]
    public async Task Rascunho_com_mod_na_fila_tambem_e_retomado()
    {
        // O processo caiu ANTES de o worker pegar o job: a versão nunca chegou a
        // Resolving. Sem cobrir este caso, um pedido feito segundos antes de um
        // deploy sumiria sem deixar vestígio.
        var (repo, queue, version) = Cenario(naFila: ["3"]);

        var retomadas = await Executar(repo, queue);

        retomadas.ShouldBe(1);
        queue.Enfileirados.Select(i => i.ProjectId).ShouldBe(["3"]);
        version.State.ShouldBe(ModpackVersionState.Draft);
    }

    [Fact]
    public async Task Item_na_fila_preserva_origem_lado_e_release_fixada()
    {
        var (repo, queue, version) = Cenario(naFila: ["3"]);
        version.PendingMods.Single().FileId = "333";
        version.PendingMods.Single().Side = FileSide.ServerOnly;

        await Executar(repo, queue);

        // Reconstruir o pedido pela metade seria pior que perdê-lo: baixaria a
        // release errada, ou mandaria para o cliente um mod só de servidor.
        var item = queue.Enfileirados.Single();
        item.FileId.ShouldBe("333");
        item.Side.ShouldBe(FileSide.ServerOnly);
        item.Origin.ShouldBe(ModFileOrigin.CurseForge);
    }

    [Fact]
    public async Task Depois_do_limite_a_versao_falha_em_vez_de_repetir()
    {
        var (repo, queue, version) = Cenario(naFila: ["3"]);
        version.MarkResolving();
        version.RecoveryAttempts = ModpackVersion.MaxRecoveryAttempts;

        var retomadas = await Executar(repo, queue);

        // O freio do ciclo de queda: se o que derruba o processo é este job,
        // reenfileirá-lo a cada arranque impede o servidor de subir.
        retomadas.ShouldBe(0);
        queue.Enfileirados.ShouldBeEmpty();
        version.State.ShouldBe(ModpackVersionState.Failed);
        version.FailureReason.ShouldNotBeNull().ShouldContain("interrompida");
    }

    [Fact]
    public async Task Reparo_manual_devolve_a_cota_de_recuperacao()
    {
        var (repo, queue, version) = Cenario(naFila: ["3"]);
        version.MarkResolving();
        version.RecoveryAttempts = ModpackVersion.MaxRecoveryAttempts;
        await Executar(repo, queue);

        // O admin conserta e manda tentar de novo.
        version.RetryAfterFailure();

        version.RecoveryAttempts.ShouldBe(0);

        // E a recuperação automática volta a valer para esta versão.
        var retomadas = await Executar(repo, queue);
        retomadas.ShouldBe(1);
    }

    [Fact]
    public async Task Sem_trabalho_restante_volta_para_rascunho_em_vez_de_falhar()
    {
        // Tudo baixou e o processo caiu antes de fechar o estado. Marcar como
        // falha seria mentira — nada falhou —, e o admin perderia tempo
        // investigando um erro que não houve.
        var (repo, queue, version) = Cenario(naFila: [], baixados: ["1", "2"]);
        version.MarkResolving();

        var retomadas = await Executar(repo, queue);

        retomadas.ShouldBe(0);
        queue.Enfileirados.ShouldBeEmpty();
        version.State.ShouldBe(ModpackVersionState.Draft);
        version.FailureReason.ShouldBeNull();
    }

    [Fact]
    public async Task Redistribuicao_negada_nao_volta_para_a_fila()
    {
        var (repo, queue, version) = Cenario(naFila: ["3"]);
        version.UpsertPending(new PendingMod
        {
            ModpackVersionId = version.Id,
            ProjectSlug = "9",
            DisplayName = "Mod fechado",
            Origin = ModFileOrigin.CurseForge,
            Reason = PendingModReason.DistributionDenied
        });

        await Executar(repo, queue);

        // É decisão do autor do mod: insistir só gasta cota de API e não muda o
        // resultado.
        queue.Enfileirados.Select(i => i.ProjectId).ShouldBe(["3"]);
    }

    private static Task<int> Executar(FakeRepo repo, FakeQueue queue) =>
        new RecoverInterruptedIngestions(repo, new IngestionScheduler(repo, queue))
            .HandleAsync(CancellationToken.None);

    /// <param name="naFila">Mods pedidos e ainda não tentados (pendência Queued).</param>
    /// <param name="baixados">Mods que já viraram arquivo na versão.</param>
    private static (FakeRepo Repo, FakeQueue Queue, ModpackVersion Version) Cenario(
        string[] naFila, string[]? baixados = null)
    {
        var modpack = new Modpack
        {
            Name = "Pack",
            Slug = "pack",
            MinecraftVersion = "1.21.1",
            Loader = ModLoader.NeoForge
        };

        var version = new ModpackVersion
        {
            ModpackId = modpack.Id,
            Version = "1.0.0",
            LoaderVersion = "21.1.100"
        };

        foreach (var projectId in baixados ?? [])
        {
            version.UpsertFile(new ModpackFile
            {
                ModpackVersionId = version.Id,
                ProjectSlug = projectId,
                Path = $"mods/{projectId}.jar",
                Sha256 = new string('a', 64),
                SizeBytes = 1,
                Side = FileSide.Both,
                Origin = ModFileOrigin.CurseForge
            });
        }

        foreach (var projectId in naFila)
        {
            version.UpsertPending(new PendingMod
            {
                ModpackVersionId = version.Id,
                ProjectSlug = projectId,
                DisplayName = projectId,
                Origin = ModFileOrigin.CurseForge,
                Reason = PendingModReason.Queued
            });
        }

        return (new FakeRepo(modpack, version), new FakeQueue(), version);
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

    /// <summary>Devolve a MESMA instância, para as transições ficarem visíveis ao teste.</summary>
    private sealed class FakeRepo(Modpack modpack, ModpackVersion version) : FakeModpackRepositoryBase
    {
        public override Task<IReadOnlyList<Guid>> ListInterruptedIngestionIdsAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Guid>>([version.Id]);

        public override Task<Modpack?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<Modpack?>(modpack);

        public override Task<ModpackVersion?> GetVersionAsync(Guid versionId, CancellationToken ct) =>
            Task.FromResult<ModpackVersion?>(version);
    }
}
