using TCMine.Contracts;
using TCMine.Contracts.Identity;
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

    private readonly FakeServerConnection _connection = new();
    private readonly LauncherShellState _shell = new();

    public ValueTask DisposeAsync() => _connection.DisposeAsync();

    [Fact]
    public void Com_sessao_pede_para_manter_o_canal_aberto()
    {
        using var supervisor = Attached();

        Pair();
        SignIn();

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
        SignIn();

        using var supervisor = Attached();

        _connection.Kept.ShouldBe([Server]);
    }

    [Fact]
    public void Nao_repete_o_pedido_a_cada_mudanca_de_estado()
    {
        using var supervisor = Attached();

        Pair();
        SignIn();

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
        SignIn();
        _shell.Apply(SignInState.SignedOut());
        SignIn();

        _connection.Kept.Count.ShouldBe(2);
    }

    [Fact]
    public void Queda_do_canal_aparece_no_rodape_sem_navegar()
    {
        // Regressão: o rodapé lia o handshake do arranque e continuava a dizer
        // "Ligado" com o servidor fora do ar.
        using var supervisor = Attached();

        Pair();
        SignIn();

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
        SignIn();

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
        SignIn();
        supervisor.Dispose();

        _connection.Report(LinkStatus.Reconnecting);

        _shell.Link.ShouldNotBe(LinkStatus.Reconnecting);
    }

    private LinkSupervisor Attached()
    {
        var supervisor = new LinkSupervisor(_connection, _shell);
        supervisor.Attach();

        return supervisor;
    }

    private void Pair() =>
        _shell.Apply(new PairingState
        {
            Status = PairingStatus.Paired,
            Config = new LauncherConfig { Schema = 1, ServerUrl = Server, AzureClientId = "client" }
        });

    private void SignIn() =>
        _shell.Apply(SignInState.SignedIn(new LauncherSessionDto
        {
            UserId = Guid.CreateVersion7(), DisplayName = "Jogador", MinecraftUuid = "0123456789abcdef0123456789abcdef"
        }));
}
