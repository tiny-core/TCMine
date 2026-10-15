using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.Core.Tests.Fakes;

namespace TCMine.Launcher.Core.Tests.Modpacks;

/// <summary>O túnel vive exatamente enquanto o jogo vive — nem mais (porta aberta à toa), nem menos.</summary>
public sealed class TunneledGameProcessTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Fecha_o_tunel_quando_o_jogo_sai()
    {
        var game = new FakeGameProcess();
        var session = new FakeGameTunnel.FakeSession();
        var process = new TunneledGameProcess(game, session);

        game.Escrever("[Server thread/INFO]: Done");
        game.Terminar();

        await foreach (var _ in process.ReadOutputAsync(Ct))
            session.Closes.ShouldBe(0); // a meio do jogo, aberto

        session.Closes.ShouldBe(1);
    }

    [Fact]
    public async Task Fechar_duas_vezes_fecha_uma()
    {
        var game = new FakeGameProcess();
        var session = new FakeGameTunnel.FakeSession();
        var process = new TunneledGameProcess(game, session);
        game.Terminar();

        await foreach (var _ in process.ReadOutputAsync(Ct))
        {
        }

        process.Dispose();

        session.Closes.ShouldBe(1);
    }
}
