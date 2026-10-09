using TCMine.Contracts.Handshake;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Connectivity;

namespace TCMine.Launcher.Core.Tests.Connectivity;

/// <summary>
///     Quando o launcher se atualiza.
///     O mecanismo é do empacotador e não se testa aqui; o que se testa é a
///     decisão, que é de produto — e as três recusas abaixo são as que importam.
/// </summary>
public class UpdateLauncherTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Com_feed_e_sem_congelamento_atualiza()
    {
        var updater = new UpdaterFalso(true);

        var reiniciou = await new UpdateLauncher(updater).HandleAsync(Resposta(), Ct);

        reiniciou.ShouldBeTrue();
        updater.Feed.ShouldBe(new Uri("https://servidor.exemplo/updates/launcher/win-x64-p2/"));
    }

    [Fact]
    public async Task Congelado_pelo_administrador_nem_procura()
    {
        // O congelamento existe para um evento em curso: ninguém atualiza no
        // meio da partida. Procurar e desistir depois daria o mesmo resultado
        // hoje e seria fácil de transformar num download desnecessário amanhã.
        var updater = new UpdaterFalso(true);

        var reiniciou = await new UpdateLauncher(updater)
            .HandleAsync(Resposta() with { UpdatesFrozen = true }, Ct);

        reiniciou.ShouldBeFalse();
        updater.Feed.ShouldBeNull();
    }

    [Fact]
    public async Task Sem_handshake_nao_atualiza()
    {
        // Servidor fora do ar. Ir buscar binários por uma URL guardada que já
        // ninguém confirmou é pior do que ficar na versão atual.
        var updater = new UpdaterFalso(true);

        var reiniciou = await new UpdateLauncher(updater).HandleAsync(null, Ct);

        reiniciou.ShouldBeFalse();
        updater.Feed.ShouldBeNull();
    }

    [Fact]
    public async Task Nada_novo_no_feed_segue_a_vida()
    {
        var reiniciou = await new UpdateLauncher(new UpdaterFalso(false))
            .HandleAsync(Resposta(), Ct);

        reiniciou.ShouldBeFalse();
    }

    private static HandshakeResponse Resposta() => new()
    {
        ProtocolMin = 2,
        ProtocolMax = 2,
        ServerVersion = "1.0.0",
        ServerName = "Servidor",
        LauncherChannel = "win-x64-p2",
        LauncherFeedUrl = new Uri("https://servidor.exemplo/updates/launcher/win-x64-p2/"),
        Capabilities = [],
        AzureClientId = "client"
    };

    private sealed class UpdaterFalso(bool vaiAtualizar) : ILauncherUpdater
    {
        public Uri? Feed { get; private set; }

        public Task<bool> UpdateAsync(Uri feedUrl, CancellationToken ct)
        {
            Feed = feedUrl;
            return Task.FromResult(vaiAtualizar);
        }
    }
}
