using TCMine.Contracts;
using TCMine.Contracts.Handshake;
using TCMine.Launcher.Core.Connectivity;

namespace TCMine.Launcher.Core.Tests.Connectivity;

/// <summary>
///     O primeiro caso de uso que roda no launcher: a que servidor ele pertence.
///     Duas regras aqui não são cosméticas. O endereço não pode sair por HTTP
///     puro, porque o id_token da Microsoft trafega nessa conexão; e uma falha de
///     rede não pode apagar o pareamento, senão o jogador redigita o endereço a
///     cada oscilação de sinal.
/// </summary>
public class ServerPairingTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sem_configuracao_o_launcher_esta_nao_pareado()
    {
        var pareamento = new ServerPairing(new HandshakeFalso(), new ConfigFalso(null));

        var estado = await pareamento.ResumeAsync(Ct);

        estado.Status.ShouldBe(PairingStatus.NotPaired);
        estado.IsPaired.ShouldBeFalse();
    }

    [Fact]
    public async Task Configuracao_valida_e_servidor_no_ar_devolve_pareado()
    {
        var config = new ConfigFalso(Config("https://servidor.exemplo/"));
        var pareamento = new ServerPairing(new HandshakeFalso(Ok()), config);

        var estado = await pareamento.ResumeAsync(Ct);

        estado.IsOnline.ShouldBeTrue();
        estado.Server!.ServerName.ShouldBe("Servidor de Teste");
    }

    [Fact]
    public async Task Servidor_fora_do_ar_nao_desfaz_o_pareamento()
    {
        // A regressão que esta suíte existe para trancar: perder o config aqui
        // manda o jogador redigitar o endereço porque a rede caiu.
        var config = new ConfigFalso(Config("https://servidor.exemplo/"));

        var pareamento = new ServerPairing(
            new HandshakeFalso(new HandshakeResult(HandshakeOutcome.Unreachable, null, "sem rede")),
            config);

        var estado = await pareamento.ResumeAsync(Ct);

        estado.Status.ShouldBe(PairingStatus.Unreachable);
        estado.IsPaired.ShouldBeTrue("o endereço continua conhecido");
        estado.IsOnline.ShouldBeFalse();
    }

    [Fact]
    public async Task Protocolos_que_nao_se_cruzam_viram_incompatibilidade()
    {
        // Distinto de "inacessível" porque tentar de novo não resolve — a tela
        // precisa dizer para atualizar, não para esperar.
        var pareamento = new ServerPairing(
            new HandshakeFalso(new HandshakeResult(HandshakeOutcome.LauncherTooOld, null, "atualize")),
            new ConfigFalso(null));

        var estado = await pareamento.PairAsync("https://servidor.exemplo", Ct);

        estado.Status.ShouldBe(PairingStatus.Incompatible);
        estado.Message.ShouldBe("atualize");
    }

    [Fact]
    public async Task Parear_grava_a_configuracao_com_o_client_id_do_servidor()
    {
        // O client id vem do servidor, não do instalador: é ele quem sabe contra
        // qual app do Azure os jogadores dele autenticam.
        var config = new ConfigFalso(null);
        var pareamento = new ServerPairing(new HandshakeFalso(Ok()), config);

        var estado = await pareamento.PairAsync("servidor.exemplo", Ct);

        estado.IsOnline.ShouldBeTrue();
        config.Gravado.ShouldNotBeNull();
        config.Gravado.AzureClientId.ShouldBe("client-do-servidor");
        config.Gravado.DisplayName.ShouldBe("Servidor de Teste");
        config.Gravado.Schema.ShouldBe(1);
    }

    [Fact]
    public async Task Endereco_sem_esquema_assume_https()
    {
        var handshake = new HandshakeFalso(Ok());
        var pareamento = new ServerPairing(handshake, new ConfigFalso(null));

        await pareamento.PairAsync("  servidor.exemplo  ", Ct);

        handshake.Chamado.ShouldBe(new Uri("https://servidor.exemplo"));
    }

    [Fact]
    public async Task Http_puro_e_recusado_sem_falar_com_o_servidor()
    {
        // Recusar depois do handshake seria tarde: o pedido já teria saído em
        // claro. Por isso o teste também exige que ninguém tenha sido chamado.
        var handshake = new HandshakeFalso(Ok());
        var config = new ConfigFalso(null);
        var pareamento = new ServerPairing(handshake, config);

        var estado = await pareamento.PairAsync("http://servidor.exemplo", Ct);

        estado.Status.ShouldBe(PairingStatus.Invalid);
        estado.Message!.ShouldContain("HTTPS");
        handshake.Chamado.ShouldBeNull();
        config.Gravado.ShouldBeNull();
    }

    [Fact]
    public async Task Localhost_em_http_continua_valendo()
    {
        // A exceção que torna o desenvolvimento possível sem certificado.
        var handshake = new HandshakeFalso(Ok());
        var pareamento = new ServerPairing(handshake, new ConfigFalso(null));

        var estado = await pareamento.PairAsync("http://localhost:5144", Ct);

        estado.IsOnline.ShouldBeTrue();
    }

    [Fact]
    public async Task Endereco_vazio_pede_um_endereco_em_vez_de_estourar()
    {
        var pareamento = new ServerPairing(new HandshakeFalso(Ok()), new ConfigFalso(null));

        var estado = await pareamento.PairAsync("   ", Ct);

        estado.Status.ShouldBe(PairingStatus.Invalid);
        estado.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Servidor_sem_client_id_explica_que_o_problema_e_do_servidor()
    {
        // Um servidor recém-instalado responde ao handshake com AzureClientId
        // vazio. Sem tradução, o jogador leria a mensagem do Validate —
        // "AzureClientId ausente" — e acharia que o launcher está quebrado.
        var handshake = new HandshakeFalso(new HandshakeResult(
            HandshakeOutcome.Ok,
            Resposta(clientId: ""),
            null));

        var config = new ConfigFalso(null);
        var pareamento = new ServerPairing(handshake, config);

        var estado = await pareamento.PairAsync("https://servidor.exemplo", Ct);

        estado.Status.ShouldBe(PairingStatus.Invalid);
        estado.Message!.ShouldContain("administrador");
        estado.Message!.ShouldNotContain("AzureClientId");
        config.Gravado.ShouldBeNull("configuração sem client id não serve para nada");
    }

    // ---------- apoio ----------

    private static LauncherConfig Config(string url) => new()
    {
        Schema = 1, ServerUrl = new Uri(url), AzureClientId = "abc"
    };

    private static HandshakeResult Ok() => new(HandshakeOutcome.Ok, Resposta(), null);

    private static HandshakeResponse Resposta(string clientId = "client-do-servidor") => new()
    {
        ProtocolMin = 1,
        ProtocolMax = 1,
        ServerVersion = "1.0.0",
        ServerName = "Servidor de Teste",
        LauncherChannel = "win-x64-p1",
        LauncherFeedUrl = new Uri("https://exemplo.com/updates"),
        Capabilities = [],
        AzureClientId = clientId
    };

    private sealed class HandshakeFalso(HandshakeResult? resultado = null) : IHandshakeClient
    {
        public Uri? Chamado { get; private set; }

        public Task<HandshakeResult> PerformAsync(Uri serverUrl, CancellationToken ct)
        {
            Chamado = serverUrl;

            return Task.FromResult(
                resultado ?? new HandshakeResult(HandshakeOutcome.Unreachable, null, "sem resposta"));
        }
    }

    private sealed class ConfigFalso(LauncherConfig? inicial) : ILauncherConfigProvider
    {
        public LauncherConfig? Gravado { get; private set; }

        public Task<LauncherConfig?> TryLoadAsync(CancellationToken ct) => Task.FromResult(inicial);

        public Task SaveAsync(LauncherConfig config, CancellationToken ct)
        {
            Gravado = config;
            return Task.CompletedTask;
        }
    }
}
