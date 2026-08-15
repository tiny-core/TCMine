using TCMine.Contracts;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

/// <summary>
///     O agendador é a outra metade da recuperação: sem o rastro que ele grava,
///     não há o que recuperar. A fila vive em memória, então um mod escolhido a
///     mão e ainda não processado desaparecia por completo se o processo caísse —
///     nem o reparo sabia que ele tinha sido pedido.
/// </summary>
public sealed class IngestionSchedulerTests
{
    [Fact]
    public async Task Grava_o_pedido_como_pendencia_antes_de_enfileirar()
    {
        var (repo, queue, version) = Cenario();

        await new IngestionScheduler(repo, queue).ScheduleAsync(
            version, [Item("jei"), Item("sodium")], CancellationToken.None);

        version.PendingMods
            .Where(p => p.Reason is PendingModReason.Queued)
            .Select(p => p.ProjectSlug)
            .Order()
            .ShouldBe(["jei", "sodium"]);

        queue.Enfileirados.Select(i => i.ProjectId).Order().ShouldBe(["jei", "sodium"]);
    }

    [Fact]
    public async Task Grava_antes_de_enfileirar_e_nao_depois()
    {
        var (repo, queue, version) = Cenario();

        await new IngestionScheduler(repo, queue).ScheduleAsync(
            version, [Item("jei")], CancellationToken.None);

        // A ordem é a garantia: entre gravar e enfileirar existe uma janela em
        // que o processo pode cair. Invertida, o job entraria na fila sem
        // rastro no banco — exatamente o buraco que isto veio fechar.
        repo.Eventos.ShouldBe(["gravou", "enfileirou"]);
    }

    [Fact]
    public async Task Nao_duplica_quando_o_mod_ja_tinha_pendencia()
    {
        var (repo, queue, version) = Cenario();
        version.UpsertPending(new PendingMod
        {
            ModpackVersionId = version.Id,
            ProjectSlug = "jei",
            DisplayName = "Just Enough Items",
            Origin = ModFileOrigin.Modrinth,
            Reason = PendingModReason.Transient
        });

        await new IngestionScheduler(repo, queue).ScheduleAsync(
            version, [Item("jei")], CancellationToken.None);

        // UpsertPending casa por ProjectSlug: dois .jar do mesmo mod em mods/
        // crashariam o jogo, e duas pendências do mesmo mod confundiriam a tela.
        version.PendingMods.Count.ShouldBe(1);
        version.PendingMods.Single().Reason.ShouldBe(PendingModReason.Queued);
    }

    [Fact]
    public async Task Lista_vazia_nao_grava_nem_enfileira()
    {
        var (repo, queue, version) = Cenario();

        await new IngestionScheduler(repo, queue).ScheduleAsync(version, [], CancellationToken.None);

        version.PendingMods.ShouldBeEmpty();
        repo.Eventos.ShouldBeEmpty();
        queue.Enfileirados.ShouldBeEmpty();
    }

    private static ModIngestionItem Item(string projectId) =>
        new(ModFileOrigin.Modrinth, projectId, null, FileSide.Both);

    private static (FakeRepo Repo, FakeQueue Queue, ModpackVersion Version) Cenario()
    {
        var version = new ModpackVersion
        {
            ModpackId = Guid.CreateVersion7(),
            Version = "1.0.0",
            LoaderVersion = "21.1.100"
        };

        var repo = new FakeRepo();
        return (repo, new FakeQueue(repo), version);
    }

    /// <summary>Registra a ordem das duas operações que precisam acontecer nesta sequência.</summary>
    private sealed class FakeRepo : FakeModpackRepositoryBase
    {
        public List<string> Eventos { get; } = [];

        public override Task UpdateVersionAsync(ModpackVersion version, CancellationToken ct)
        {
            Eventos.Add("gravou");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeQueue(FakeRepo repo) : IIngestionQueue
    {
        public List<ModIngestionItem> Enfileirados { get; } = [];

        public ValueTask EnqueueAsync(Guid versionId, IReadOnlyList<ModIngestionItem> items, CancellationToken ct)
        {
            repo.Eventos.Add("enfileirou");
            Enfileirados.AddRange(items);
            return ValueTask.CompletedTask;
        }
    }
}
