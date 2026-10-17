using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.Core.Sync;
using TCMine.Launcher.Core.Tests.Fakes;
using TCMine.Launcher.UI.Abstractions;
using TCMine.Launcher.UI.State;

namespace TCMine.Launcher.Core.Tests.UI;

/// <summary>
///     A janela sai do caminho quando o jogo abre e volta quando ele fecha — e
///     só nas transições, não a cada linha de log.
/// </summary>
public sealed class GameWindowCoordinatorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Minimiza_ao_abrir_e_volta_ao_fechar()
    {
        var game = new GameSession();
        var chrome = new MolduraFalsa();
        using var coordinator = new GameWindowCoordinator(game, chrome);
        var process = new FakeGameProcess();

        game.Attach(Instalada(), process);
        for (var i = 0; i < 20; i++)
            process.Escrever($"linha {i}");

        await EsperarAsync(() => game.Log.Count == 20);
        chrome.Minimizacoes.ShouldBe(1);

        process.Terminar();
        await EsperarAsync(() => chrome.Restauracoes == 1);
    }

    [Fact]
    public void Jogo_reencontrado_nao_minimiza()
    {
        // Quem acabou de abrir o launcher quer vê-lo.
        var game = new GameSession();
        var chrome = new MolduraFalsa();
        using var coordinator = new GameWindowCoordinator(game, chrome);

        game.Reattach(Instalada(), new FakeGameProcess());

        chrome.Minimizacoes.ShouldBe(0);
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
            ManagedFiles = new Dictionary<string, string>()
        },
        0,
        "/instancias/pack");

    private sealed class MolduraFalsa : IWindowChrome
    {
        private int _minimizacoes;
        private int _restauracoes;

        public int Minimizacoes => Volatile.Read(ref _minimizacoes);

        public int Restauracoes => Volatile.Read(ref _restauracoes);

        public void BeginDrag()
        {
        }

        public void Minimize() => Interlocked.Increment(ref _minimizacoes);

        public void Restore() => Interlocked.Increment(ref _restauracoes);

        public void Close()
        {
        }
    }
}
