using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.Core.Sync;
using TCMine.Launcher.Core.Tests.Fakes;

namespace TCMine.Launcher.Core.Tests.Modpacks;

/// <summary>
///     O launcher reaberto com o jogo a correr. Errar para um lado é oferecer
///     "Jogar" com o jogo aberto (duas cópias no mesmo mundo); para o outro, é
///     acreditar num registo velho e mostrar "a jogar" para sempre.
/// </summary>
public class ResumeRunningGameTests
{
    private static readonly GameProcessIdentity Identity = new(4242, DateTimeOffset.UtcNow);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sem_registo_nao_ha_o_que_reencontrar()
    {
        var session = new GameSession();

        var found = await Build(new FakeRunningGameStore(), new FakeInstanceStore(), session).HandleAsync(Ct);

        found.ShouldBeFalse();
        session.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public async Task Jogo_ainda_aberto_volta_para_a_sessao()
    {
        var key = InstanceKey.New();
        var instances = new FakeInstanceStore { Manifests = { [key] = Manifesto() } };
        var store = new FakeRunningGameStore { Record = new RunningGameRecord(key, Identity) };
        var session = new GameSession();

        var found = await Build(store, instances, session, new FakeGameProcess()).HandleAsync(Ct);

        found.ShouldBeTrue();
        session.Running!.Key.ShouldBe(key);
        session.IsReattached.ShouldBeTrue();
        store.Record.ShouldNotBeNull(); // o registo continua a ser o dele
    }

    [Fact]
    public async Task Jogo_que_ja_saiu_apaga_o_registo_velho()
    {
        var key = InstanceKey.New();
        var instances = new FakeInstanceStore { Manifests = { [key] = Manifesto() } };
        var store = new FakeRunningGameStore { Record = new RunningGameRecord(key, Identity) };
        var session = new GameSession();

        // Sem processo vivo para essa identidade (saiu, ou o PID é de outro).
        var found = await Build(store, instances, session).HandleAsync(Ct);

        found.ShouldBeFalse();
        session.IsRunning.ShouldBeFalse();
        store.Record.ShouldBeNull();
    }

    [Fact]
    public async Task Instancia_removida_apaga_o_registo()
    {
        var store = new FakeRunningGameStore { Record = new RunningGameRecord(InstanceKey.New(), Identity) };

        var found = await Build(store, new FakeInstanceStore(), new GameSession(), new FakeGameProcess())
            .HandleAsync(Ct);

        found.ShouldBeFalse();
        store.Record.ShouldBeNull();
    }

    private static ResumeRunningGame Build(
        FakeRunningGameStore store, FakeInstanceStore instances, GameSession session, IGameProcess? alive = null) =>
        new(store, new FakeGameProcessFinder(alive is null ? null : Identity, alive), instances, session);

    private static InstanceManifest Manifesto() => new()
    {
        Schema = 2,
        ModpackId = Guid.CreateVersion7(),
        ModpackVersionId = Guid.CreateVersion7(),
        ModpackName = "Pack",
        Version = "1.0.0",
        InstalledAt = DateTimeOffset.UtcNow,
        ManagedFiles = new Dictionary<string, string>(),
        MinecraftVersion = "1.21.1",
        Loader = ModLoader.NeoForge
    };
}
