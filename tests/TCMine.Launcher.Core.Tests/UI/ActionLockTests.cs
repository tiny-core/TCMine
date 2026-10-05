using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.Core.Sync;
using TCMine.Launcher.Core.Tests.Fakes;
using TCMine.Launcher.UI.State;

namespace TCMine.Launcher.Core.Tests.UI;

/// <summary>
///     Com o jogo aberto, as ações do launcher ficam desligadas até ele fechar —
///     atualizar ou remover uma instância mexeria em arquivos que o Java lê.
/// </summary>
public sealed class ActionLockTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Trava_enquanto_o_jogo_esta_aberto_e_solta_quando_fecha()
    {
        var game = new GameSession();
        using var trava = new ActionLock(new InstallOperationState(), game);
        var process = new FakeGameProcess();

        trava.IsLocked.ShouldBeFalse();

        game.Attach(Instalada(), process);
        trava.IsLocked.ShouldBeTrue();
        trava.Reason.ShouldBe("Feche o jogo para usar isto.");

        process.Terminar();
        await EsperarAsync(() => !trava.IsLocked);
    }

    [Fact]
    public async Task Avisa_so_quando_a_trava_muda_e_nao_a_cada_linha_de_log()
    {
        // O jogo escreve milhares de linhas; repassar cada uma faria todas as
        // telas redesenharem a cada linha.
        var game = new GameSession();
        using var trava = new ActionLock(new InstallOperationState(), game);
        var avisos = 0;
        trava.Changed += () => Interlocked.Increment(ref avisos);
        var process = new FakeGameProcess();

        game.Attach(Instalada(), process);
        for (var i = 0; i < 50; i++)
            process.Escrever($"linha {i}");

        await EsperarAsync(() => game.Log.Count == 50);
        process.Terminar();
        await EsperarAsync(() => !trava.IsLocked);

        avisos.ShouldBe(2); // abriu, fechou
    }

    [Fact]
    public void Instalacao_em_curso_tambem_trava()
    {
        var operation = new InstallOperationState();
        using var trava = new ActionLock(operation, new GameSession());

        operation.Begin(null);

        trava.IsLocked.ShouldBeTrue();
        trava.Reason.ShouldBe("Espere a instalação em curso terminar.");
    }

    private static async Task EsperarAsync(Func<bool> condicao)
    {
        for (var i = 0; i < 200 && !condicao(); i++)
            await Task.Delay(10, Ct);

        condicao().ShouldBeTrue("não chegou ao estado esperado a tempo");
    }

    private static InstalledInstance Instalada() => new(
        InstanceKey.New(),
        new InstanceManifest
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
        },
        SizeBytes: 0,
        Path: "/instancias/pack");
}
