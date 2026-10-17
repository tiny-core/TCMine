using TCMine.Contracts.Identity;
using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Options;
using TCMine.Launcher.Core.Sync;
using TCMine.Launcher.Core.Tests.Fakes;

namespace TCMine.Launcher.Core.Tests.Options;

/// <summary>
///     Quem vence entre este PC e a conta. A regra é "o que mudou por último",
///     e "mudou" é contra a última sincronização desta instância — é isso que
///     impede um jogo fechado sem rede de ter as teclas atropeladas na abertura
///     seguinte.
/// </summary>
public class SyncPlayerOptionsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Primeira_vez_neste_pack_envia_as_teclas_daqui()
    {
        await using var c = new Cenario();
        c.Local("key_key.jump:key.keyboard.space");

        await c.Sync.BeforeLaunchAsync(c.Instance, Ct);

        c.Remote!.KeyBindings.ShouldBe("key_key.jump:key.keyboard.space");
        c.Store.States.ShouldContainKey(c.Instance.Key);
    }

    [Fact]
    public async Task O_que_outro_PC_enviou_chega_antes_de_abrir()
    {
        await using var c = new Cenario();
        c.Local("key_key.jump:key.keyboard.space\nfov:0.5");
        await c.Sync.BeforeLaunchAsync(c.Instance, Ct); // sincronizado: a conta tem o mesmo

        c.OutroPcEnvia("key_key.jump:key.keyboard.j");

        await c.Sync.BeforeLaunchAsync(c.Instance, Ct);

        c.Store.Files[c.Instance.Key].Text.ShouldContain("key_key.jump:key.keyboard.j");
        c.Store.Files[c.Instance.Key].Text.ShouldContain("fov:0.5");
    }

    [Fact]
    public async Task Jogo_que_fechou_sem_rede_envia_na_abertura_seguinte()
    {
        // Sem esta regra, abrir de novo trazia da conta a tecla VELHA por cima
        // da que o jogador acabou de mudar.
        await using var c = new Cenario();
        c.Local("key_key.jump:key.keyboard.space");
        await c.Sync.BeforeLaunchAsync(c.Instance, Ct);

        c.Local("key_key.jump:key.keyboard.j"); // mudou aqui, e o envio falhou

        await c.Sync.BeforeLaunchAsync(c.Instance, Ct);

        c.Remote!.KeyBindings.ShouldBe("key_key.jump:key.keyboard.j");
        c.Store.Files[c.Instance.Key].Text.ShouldContain("key.keyboard.j");
    }

    [Fact]
    public async Task Mudou_nos_dois_vence_o_mais_recente()
    {
        await using var c = new Cenario();
        c.Local("key_key.jump:key.keyboard.space");
        await c.Sync.BeforeLaunchAsync(c.Instance, Ct);

        c.Local("key_key.jump:key.keyboard.j", DateTimeOffset.UtcNow.AddHours(-1)); // daqui, mais velho
        c.OutroPcEnvia("key_key.jump:key.keyboard.k");                             // de lá, agora

        await c.Sync.BeforeLaunchAsync(c.Instance, Ct);

        c.Store.Files[c.Instance.Key].Text.ShouldContain("key.keyboard.k");
    }

    [Fact]
    public async Task Ao_fechar_envia_so_se_mudou()
    {
        await using var c = new Cenario();
        c.Local("key_key.jump:key.keyboard.space");
        await c.Sync.BeforeLaunchAsync(c.Instance, Ct);
        var antes = c.Remote!.UpdatedAt;

        await c.Sync.AfterExitAsync(c.Instance, Ct);
        c.Remote!.UpdatedAt.ShouldBe(antes);

        c.Local("key_key.jump:key.keyboard.j");
        await c.Sync.AfterExitAsync(c.Instance, Ct);

        c.Remote!.KeyBindings.ShouldBe("key_key.jump:key.keyboard.j");
    }

    [Fact]
    public async Task So_teclas_nao_manda_os_graficos()
    {
        await using var c = new Cenario();
        c.Local("key_key.jump:key.keyboard.space\nrenderDistance:12");

        await c.Sync.BeforeLaunchAsync(c.Instance, Ct);

        c.Remote!.Options.ShouldBeNull();
    }

    [Fact]
    public async Task Com_tudo_ligado_os_graficos_vao_junto()
    {
        await using var c = new Cenario(syncAll: true);
        c.Local("key_key.jump:key.keyboard.space\nrenderDistance:12");

        await c.Sync.BeforeLaunchAsync(c.Instance, Ct);

        c.Remote!.Options.ShouldBe("renderDistance:12");
    }

    [Fact]
    public async Task Sem_servidor_o_jogo_abre_com_o_que_esta_no_disco()
    {
        await using var c = new Cenario(fora: true);
        c.Local("key_key.jump:key.keyboard.space");

        await Should.NotThrowAsync(() => c.Sync.BeforeLaunchAsync(c.Instance, Ct));
        await Should.NotThrowAsync(() => c.Sync.AfterExitAsync(c.Instance, Ct));

        c.Store.Files[c.Instance.Key].Text.ShouldBe("key_key.jump:key.keyboard.space");
    }

    private sealed class Cenario : IAsyncDisposable
    {
        private readonly FakeServerConnection _connection = new();

        public Cenario(bool syncAll = false, bool fora = false)
        {
            if (fora)
                _connection.Throws = new InvalidOperationException("sem canal");

            Sync = new SyncPlayerOptions(_connection, Store, new FakeLauncherPreferences(syncAll));
        }

        public ValueTask DisposeAsync() => _connection.DisposeAsync();

        public FakePlayerOptionsStore Store { get; } = new();

        public SyncPlayerOptions Sync { get; }

        public InstalledInstance Instance { get; } = new(
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

        public PlayerModpackSettingsDto? Remote =>
            _connection.Settings.GetValueOrDefault(Instance.Manifest.ModpackId);

        public void Local(string text, DateTimeOffset? at = null) =>
            Store.Files[Instance.Key] = new LocalOptions(text, at ?? DateTimeOffset.UtcNow);

        public void OutroPcEnvia(string keys) =>
            _connection.Settings[Instance.Manifest.ModpackId] = new PlayerModpackSettingsDto
            {
                KeyBindings = keys, UpdatedAt = DateTimeOffset.UtcNow.AddSeconds(1)
            };
    }
}
