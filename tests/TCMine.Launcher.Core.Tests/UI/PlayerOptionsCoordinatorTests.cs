using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.Core.Options;
using TCMine.Launcher.Core.Sync;
using TCMine.Launcher.Core.Tests.Fakes;
using TCMine.Launcher.UI.State;

namespace TCMine.Launcher.Core.Tests.UI;

/// <summary>As teclas vão para a conta quando o jogo fecha, venha ele de que tela vier.</summary>
public sealed class PlayerOptionsCoordinatorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Fechar_o_jogo_envia_as_teclas()
    {
        await using var connection = new FakeServerConnection();
        var store = new FakePlayerOptionsStore();
        var game = new GameSession();
        using var coordinator = new PlayerOptionsCoordinator(
            game, new SyncPlayerOptions(connection, store, new FakeLauncherPreferences()));

        var instance = Instalada();
        var process = new FakeGameProcess();
        game.Attach(instance, process);

        // Espera o AVISO de fechado, e não o IsRunning: o GameSession zera o
        // Running antes de avisar (ainda limpa o processo e o registo no meio),
        // e o teste que olhava o IsRunning às vezes esperava o Pending antigo,
        // já concluído, antes de o envio começar. Este ouvinte entra depois do
        // coordenador, e os ouvintes de um evento correm na ordem em que
        // entraram: quando ele dispara, o Pending já é o envio novo.
        var fechou = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        game.Changed += () =>
        {
            if (!game.IsRunning)
                fechou.TrySetResult();
        };

        // O jogador mudou a tecla durante o jogo.
        store.Files[instance.Key] = new LocalOptions("key_key.jump:key.keyboard.j", DateTimeOffset.UtcNow);

        process.Terminar();

        await fechou.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        await coordinator.Pending;

        connection.Settings[instance.Manifest.ModpackId].KeyBindings.ShouldBe("key_key.jump:key.keyboard.j");
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
        0,
        "/instancias/pack");
}
