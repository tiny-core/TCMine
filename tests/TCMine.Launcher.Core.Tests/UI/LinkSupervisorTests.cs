using Microsoft.Extensions.Logging.Abstractions;
using TCMine.Contracts;
using TCMine.Contracts.Handshake;
using TCMine.Contracts.Identity;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Connectivity;
using TCMine.Launcher.Core.Identity;
using TCMine.Launcher.Core.Tests.Fakes;
using TCMine.Launcher.UI.State;

namespace TCMine.Launcher.Core.Tests.UI;

/// <summary>
///     O canal abre quando há sessão, e o rodapé acompanha o que acontece com
///     ele — em vez de repetir a resposta do handshake do arranque.
/// </summary>
public sealed class LinkSupervisorTests : IAsyncDisposable
{
    private static readonly Uri Server = new("https://modpacks.exemplo/");

    private static readonly LauncherConfig Config = new()
    {
        Schema = 1, ServerUrl = Server, AzureClientId = "client", DisplayName = "Servidor"
    };

    private static readonly LauncherSessionDto Session = new()
    {
        UserId = Guid.CreateVersion7(), DisplayName = "Jogador", MinecraftUuid = "0123456789abcdef0123456789abcdef"
    };

    private readonly FakeAuthenticator _authenticator = new();
    private readonly FakeServerConnection _connection = new();
    private readonly FakeHandshake _handshake = new();
    private readonly LauncherShellState _shell = new();
    private readonly FakeUpdater _updater = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => _connection.DisposeAsync();

    [Fact]
    public void Com_sessao_pede_para_manter_o_canal_aberto()
    {
        using var supervisor = Attached();

        Pair();
        LogIn();

        _connection.Kept.ShouldBe([Server]);
    }

    [Fact]
    public void Sem_sessao_nao_abre_canal()
    {
        // O hub exige login: tentar antes só encheria o log de recusas.
        using var supervisor = Attached();

        Pair();

        _connection.Kept.ShouldBeEmpty();
    }

    [Fact]
    public void Sessao_que_ja_existia_ao_anexar_tambem_conta()
    {
        Pair();
        LogIn();

        using var supervisor = Attached();

        _connection.Kept.ShouldBe([Server]);
    }

    [Fact]
    public void Nao_repete_o_pedido_a_cada_mudanca_de_estado()
    {
        using var supervisor = Attached();

        Pair();
        LogIn();

        _connection.Report(LinkStatus.Reconnecting);
        _connection.Report(LinkStatus.Up);
        _shell.FinishStartup();

        _connection.Kept.Count.ShouldBe(1);
    }

    [Fact]
    public void Entrar_de_novo_depois_de_sair_volta_a_pedir()
    {
        using var supervisor = Attached();

        Pair();
        LogIn();
        _shell.Apply(SignInState.SignedOut());
        LogIn();

        _connection.Kept.Count.ShouldBe(2);
    }

    [Fact]
    public void Queda_do_canal_aparece_no_rodape_sem_navegar()
    {
        // Regressão: o rodapé lia o handshake do arranque e continuava a dizer
        // "Ligado" com o servidor fora do ar.
        using var supervisor = Attached();

        Pair();
        LogIn();

        _connection.Report(LinkStatus.Up);
        _shell.Connection.ShouldBe(ConnectionState.Connected);

        _connection.Report(LinkStatus.Reconnecting);
        _shell.Connection.ShouldBe(ConnectionState.Reconnecting);

        _connection.Report(LinkStatus.Down);
        _shell.Connection.ShouldBe(ConnectionState.Disconnected);

        _connection.Report(LinkStatus.Up);
        _shell.Connection.ShouldBe(ConnectionState.Connected);
    }

    [Fact]
    public void Sem_sessao_o_rodape_vale_se_do_handshake()
    {
        // Na tela de login ainda não há canal; o servidor respondeu ao
        // handshake, e é isso que "Ligado" quer dizer ali.
        using var supervisor = Attached();

        Pair();

        _shell.Link.ShouldBe(LinkStatus.Down);
        _shell.Connection.ShouldBe(ConnectionState.Connected);
    }

    [Fact]
    public void Avisa_as_telas_quando_o_canal_muda()
    {
        using var supervisor = Attached();

        Pair();
        LogIn();

        var avisos = 0;
        _shell.Changed += () => avisos++;

        _connection.Report(LinkStatus.Reconnecting);
        _connection.Report(LinkStatus.Reconnecting);

        // Um só: repetir o mesmo estado não é mudança, e cada aviso é um
        // redesenho de todas as telas abertas.
        avisos.ShouldBe(1);
    }

    [Fact]
    public void Depois_de_descartado_deixa_de_ouvir()
    {
        var supervisor = Attached();

        Pair();
        LogIn();
        supervisor.Dispose();

        _connection.Report(LinkStatus.Reconnecting);

        _shell.Link.ShouldNotBe(LinkStatus.Reconnecting);
    }

    [Fact]
    public void Servidor_fora_no_arranque_fica_a_tentar_de_novo()
    {
        // Regressão: o arranque desistia aqui, e o launcher ficava desligado
        // até ser fechado e reaberto.
        using var supervisor = Attached();

        StartOffline();

        _shell.IsRecovering.ShouldBeTrue();
        _shell.Connection.ShouldBe(ConnectionState.Reconnecting);
    }

    [Fact]
    public async Task Servidor_que_volta_retoma_a_sessao_sozinho()
    {
        using var supervisor = Attached();

        StartOffline();
        _handshake.Result = Ok();

        (await supervisor.SyncWithServerAsync(Ct)).ShouldBeTrue();

        _shell.IsOnline.ShouldBeTrue();
        _shell.IsSignedIn.ShouldBeTrue();
        _connection.Kept.ShouldBe([Server]);
    }

    [Fact]
    public async Task Servidor_ainda_fora_continua_offline()
    {
        using var supervisor = Attached();

        StartOffline();

        (await supervisor.SyncWithServerAsync(Ct)).ShouldBeFalse();

        _shell.IsOffline.ShouldBeTrue();
        _connection.Kept.ShouldBeEmpty();
    }

    [Fact]
    public async Task Sem_credencial_guardada_pede_o_login()
    {
        using var supervisor = Attached();

        string? rota = null;
        supervisor.NavigationRequested += r => rota = r;

        StartOffline();
        _handshake.Result = Ok();
        _authenticator.Result = AuthResult.NoStoredCredentials();

        (await supervisor.SyncWithServerAsync(Ct)).ShouldBeTrue();

        _shell.IsSignedIn.ShouldBeFalse();
        rota.ShouldBe("/login");
    }

    [Fact]
    public async Task Atualizacao_descoberta_a_meio_vira_aviso_e_nao_reinicia()
    {
        // Reiniciar sozinho a meio de uma sessão fecharia o launcher por baixo
        // de um jogo aberto.
        using var supervisor = Attached();

        Pair();
        LogIn();
        _handshake.Result = Ok();
        _updater.Available = "1.2.0";

        await supervisor.SyncWithServerAsync(Ct);

        _shell.PendingUpdate.ShouldBe("1.2.0");
        _updater.Applied.ShouldBeFalse();
    }

    [Fact]
    public void Canal_que_volta_refaz_o_handshake()
    {
        // É depois de o servidor reiniciar que o que ele oferece pode ter mudado.
        using var supervisor = Attached();

        Pair();
        LogIn();
        _handshake.Result = Ok();

        _connection.Report(LinkStatus.Up);
        _handshake.Calls.ShouldBe(0);

        _connection.Report(LinkStatus.Reconnecting);
        _connection.Report(LinkStatus.Up);

        _handshake.Calls.ShouldBe(1);
    }

    [Fact]
    public void Handshake_que_falha_com_o_canal_de_pe_nao_desliga_os_menus()
    {
        using var supervisor = Attached();

        Pair();
        LogIn();

        _connection.Report(LinkStatus.Up);
        _connection.Report(LinkStatus.Reconnecting);
        _connection.Report(LinkStatus.Up);

        _handshake.Calls.ShouldBe(1);
        _shell.IsOnline.ShouldBeTrue();
        _shell.Connection.ShouldBe(ConnectionState.Connected);
    }

    private LinkSupervisor Attached()
    {
        var supervisor = new LinkSupervisor(
            _connection,
            _shell,
            new ServerPairing(_handshake, new FakeConfig()),
            new SignIn(_authenticator, new FakeSessionApi(), _connection),
            new UpdateLauncher(_updater),
            NullLogger<LinkSupervisor>.Instance,

            // Nunca termina sozinha: o laço de recuperação fica parado na
            // espera, e cada teste decide quando "tentar de novo" acontece.
            (_, ct) => Task.Delay(Timeout.InfiniteTimeSpan, ct));

        supervisor.Attach();

        return supervisor;
    }

    private void Pair() =>
        _shell.Apply(new PairingState { Status = PairingStatus.Paired, Config = Config });

    private void LogIn() => _shell.Apply(SignInState.SignedIn(Session));

    /// <summary>O arranque encontrou o servidor fora do ar, com pareamento gravado.</summary>
    private void StartOffline()
    {
        _shell.Apply(new PairingState { Status = PairingStatus.Unreachable, Config = Config });
        _shell.FinishStartup();
    }

    private static HandshakeResult Ok() => new(
        HandshakeOutcome.Ok,
        new HandshakeResponse
        {
            ProtocolMin = Protocol.Current,
            ProtocolMax = Protocol.Current,
            ServerVersion = "1.0.0",
            ServerName = "Servidor",
            LauncherChannel = "win-x64-p2",
            LauncherFeedUrl = new Uri("https://modpacks.exemplo/updates/"),
            Capabilities = [],
            AzureClientId = "client"
        },
        null);

    private sealed class FakeHandshake : IHandshakeClient
    {
        public HandshakeResult Result { get; set; } = new(HandshakeOutcome.Unreachable, null, "sem resposta");

        public int Calls { get; private set; }

        public Task<HandshakeResult> PerformAsync(Uri serverUrl, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeConfig : ILauncherConfigProvider
    {
        public Task<LauncherConfig?> TryLoadAsync(CancellationToken ct) => Task.FromResult<LauncherConfig?>(Config);

        public Task SaveAsync(LauncherConfig config, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeAuthenticator : IMinecraftAuthenticator
    {
        public AuthResult Result { get; set; } = AuthResult.Success("token");

        public Task<AuthResult> TrySilentAsync(string azureClientId, CancellationToken ct) => Task.FromResult(Result);

        public Task<AuthResult> SignInAsync(string azureClientId, CancellationToken ct) => Task.FromResult(Result);

        public Task SignOutAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeSessionApi : ILauncherSessionApi
    {
        public Task<SessionResult> SignInAsync(Uri serverUrl, string minecraftAccessToken, CancellationToken ct) =>
            Task.FromResult(SessionResult.Success(Session));

        public Task SignOutAsync(Uri serverUrl, CancellationToken ct) => Task.CompletedTask;

        public Task<InviteRedeemResult> RedeemInviteAsync(Uri serverUrl, string code, CancellationToken ct) =>
            Task.FromResult(InviteRedeemResult.Failed("não usado neste teste"));
    }

    private sealed class FakeUpdater : ILauncherUpdater
    {
        public string? Available { get; set; }

        public bool Applied { get; private set; }

        public Task<bool> UpdateAsync(Uri feedUrl, CancellationToken ct)
        {
            Applied = true;
            return Task.FromResult(true);
        }

        public Task<string?> FindUpdateAsync(Uri feedUrl, CancellationToken ct) => Task.FromResult(Available);
    }
}
