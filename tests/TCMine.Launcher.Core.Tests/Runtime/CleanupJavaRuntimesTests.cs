using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.Core.Runtime;
using TCMine.Launcher.Core.Sync;
using TCMine.Launcher.Core.Tests.Fakes;

namespace TCMine.Launcher.Core.Tests.Runtime;

/// <summary>
///     Apagar os JREs que ninguém pede.
///     Eles acumulam sozinhos — um pack que salta de 21 para 25 deixa o 21 no
///     disco para sempre —, e o que este caso de uso não pode fazer é apagar um
///     que ainda serve, ou apagar com o jogo aberto.
/// </summary>
public class CleanupJavaRuntimesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Apaga_o_que_nenhuma_instancia_pede()
    {
        var java = new FakeJavaLocator { Installed = { [21] = 100, [25] = 200 } };

        var freed = await Build(java, declarado: 25, "26.2").HandleAsync(Ct);

        java.Removed.ShouldBe([21]);
        freed.ShouldBe(100);
    }

    [Fact]
    public async Task Nao_apaga_o_que_a_instancia_ainda_usa()
    {
        var java = new FakeJavaLocator { Installed = { [21] = 100 } };

        await Build(java, declarado: 21, "1.21.1").HandleAsync(Ct);

        java.Removed.ShouldBeEmpty();
    }

    [Fact]
    public async Task Com_o_jogo_aberto_nao_apaga_nada()
    {
        // O processo em execução corre a partir de uma destas pastas: puxá-la
        // debaixo dele mata a partida do jogador.
        var java = new FakeJavaLocator { Installed = { [21] = 100 } };
        var session = new GameSession();

        session.Attach(Instalada("26.2"), new FakeGameProcess());

        var freed = await Build(java, declarado: 25, "26.2", session).HandleAsync(Ct);

        java.Removed.ShouldBeEmpty();
        freed.ShouldBe(0);
    }

    [Fact]
    public async Task Sem_instancia_nenhuma_todos_sobram()
    {
        // Desinstalar o último pack é exatamente quando há mais a libertar.
        var java = new FakeJavaLocator { Installed = { [21] = 100, [25] = 200 } };

        var caso = new CleanupJavaRuntimes(
            new FakeInstanceStore(), new ExigenciaFalsa(null), java, new GameSession());

        (await caso.HandleAsync(Ct)).ShouldBe(300);
    }

    [Fact]
    public async Task Sem_rede_o_palpite_decide_e_nao_estoura()
    {
        // A fonte autoritativa devolve nulo offline. Cair para o palpite é o
        // mesmo que o arranque do jogo faz, então os dois concordam — e o preço
        // de divergirem é uma descarga, não uma falha.
        var java = new FakeJavaLocator { Installed = { [17] = 100, [25] = 200 } };

        await Build(java, declarado: null, "26.2").HandleAsync(Ct);

        java.Removed.ShouldBe([17]);
    }

    [Fact]
    public async Task Listar_nao_apaga()
    {
        // A tela mostra o número antes de perguntar; contar não pode agir.
        var java = new FakeJavaLocator { Installed = { [21] = 100, [25] = 200 } };

        var unused = await Build(java, declarado: 25, "26.2").FindUnusedAsync(Ct);

        unused.Select(r => r.MajorVersion).ShouldBe([21]);
        java.Removed.ShouldBeEmpty();
    }

    // ---------- apoio ----------

    private static CleanupJavaRuntimes Build(
        FakeJavaLocator java,
        int? declarado,
        string minecraft,
        GameSession? session = null)
    {
        var instances = new FakeInstanceStore();
        var installed = Instalada(minecraft);

        instances.Manifests[installed.Key] = installed.Manifest;

        return new CleanupJavaRuntimes(
            instances, new ExigenciaFalsa(declarado), java, session ?? new GameSession());
    }

    private static InstalledInstance Instalada(string minecraft) =>
        new(InstanceKey.New(),
            new InstanceManifest
            {
                Schema = 2,
                ModpackId = Guid.CreateVersion7(),
                ModpackVersionId = Guid.CreateVersion7(),
                ModpackName = "Pack",
                Version = "1.0.0",
                InstalledAt = DateTimeOffset.UtcNow,
                ManagedFiles = new Dictionary<string, string>(),
                MinecraftVersion = minecraft,
                Loader = ModLoader.NeoForge
            },
            SizeBytes: 0,
            Path: "/instancias/pack");

    private sealed class ExigenciaFalsa(int? declarado) : IJavaRequirementSource
    {
        public Task<int?> GetRequiredJavaAsync(string minecraftVersion, CancellationToken ct) =>
            Task.FromResult(declarado);
    }
}
