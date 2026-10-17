using TCMine.Contracts;
using TCMine.Contracts.Modpacks;
using TCMine.Contracts.Servers;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.Core.Sync;
using TCMine.Launcher.Core.Tests.Fakes;

namespace TCMine.Launcher.Core.Tests.Modpacks;

/// <summary>
///     A ordem e as recusas de abrir o jogo.
///     O valor deste caso de uso não é abrir o processo — isso é da porta. É
///     dizer QUAL das quatro coisas faltou, porque "não foi possível abrir o
///     jogo" não diz ao jogador se ele deve entrar de novo, reinstalar, ou
///     esperar a rede voltar.
/// </summary>
public class LaunchGameTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task O_caminho_feliz_entrega_token_e_java_ao_motor()
    {
        var motor = new MotorFalso();

        var result = await Build(motor: motor).HandleAsync(
            Instalada(), Config(), null, Ct);

        result.Started.ShouldBeTrue();

        var request = motor.Recebido.ShouldNotBeNull();
        request.AccessToken.ShouldBe("token-do-minecraft");
        request.JavaPath.ShouldBe("/runtimes/21/bin/java");
        request.MinecraftVersion.ShouldBe("1.21.1");
        request.Loader.ShouldBe(ModLoader.NeoForge);
        request.PlayerUuid.ShouldBe("abc123");
        request.PlayerName.ShouldBe("Jogador");
    }

    [Fact]
    public async Task O_java_declarado_pela_versao_vence_o_palpite()
    {
        // O bug que este teste tranca: o Minecraft trocou de esquema de versão,
        // o palpite não entendeu "26.2" e devolveu um Java velho. O jogo morria
        // com "Could not create the Java Virtual Machine".
        var java = new FakeJavaLocator();

        await Build(java: java, javaDeclarado: 25).HandleAsync(
            Instalada("26.2"), Config(), null, Ct);

        java.Requested.ShouldBe(25);
    }

    [Fact]
    public async Task Sem_resposta_da_versao_cai_no_palpite()
    {
        // Versão desconhecida e sem rede: melhor abrir com um palpite do que não
        // abrir.
        var java = new FakeJavaLocator();

        await Build(java: java).HandleAsync(
            Instalada("1.20.4"), Config(), null, Ct);

        java.Requested.ShouldBe(17);
    }

    [Fact]
    public async Task Instancia_de_schema_antigo_manda_reinstalar()
    {
        // Sem MinecraftVersion não há o que executar, e adivinhar abriria o jogo
        // errado. A mensagem tem de nomear o pack: o jogador pode ter vários.
        var manifest = Manifesto() with { MinecraftVersion = null, Loader = null };

        var result = await Build().HandleAsync(
            Instalada(manifest), Config(), null, Ct);

        result.Started.ShouldBeFalse();
        result.Message!.ShouldContain("Reinstale");
        result.Message!.ShouldContain("Pack de Teste");
    }

    [Fact]
    public async Task Sem_conta_e_sem_perfil_guardado_manda_entrar_uma_vez()
    {
        // Primeira instalação sem nunca ter entrado: não há identidade nenhuma, e
        // "tente de novo" mandaria o jogador clicar para sempre sem resolver.
        var result = await Build(account: AuthResult.NoStoredCredentials()).HandleAsync(
            Instalada(), Config(), null, Ct);

        result.Started.ShouldBeFalse();
        result.Message!.ShouldContain("pelo menos uma vez");
    }

    [Fact]
    public async Task O_perfil_vem_do_minecraft_e_fica_guardado()
    {
        // A identidade que o jogo exige é a do Minecraft. Vinha do servidor
        // TCMine por conveniência, e por isso tê-lo fora do ar impedia jogar.
        var cache = new CacheFalso();
        var motor = new MotorFalso();

        await Build(motor: motor, cache: cache,
                perfis: new PerfilFalso(new PlayerProfile("Steve", "uuid-do-steve")))
            .HandleAsync(Instalada(), Config(), null, Ct);

        motor.Recebido!.PlayerName.ShouldBe("Steve");
        motor.Recebido.PlayerUuid.ShouldBe("uuid-do-steve");
        cache.Guardado!.Name.ShouldBe("Steve");
    }

    [Fact]
    public async Task Sem_rede_mas_com_perfil_guardado_abre_offline()
    {
        // Token nulo é o sinal de modo offline para o motor. O jogo abre para um
        // jogador só — entrar em servidor online exige prova de conta, e essa
        // regra é do Minecraft, não nossa.
        var motor = new MotorFalso();

        var result = await Build(
                motor: motor,
                account: AuthResult.Failed("sem rede"),
                cache: new CacheFalso(new PlayerProfile("Steve", "uuid-do-steve")))
            .HandleAsync(Instalada(), Config(), null, Ct);

        result.Started.ShouldBeTrue();
        motor.Recebido!.AccessToken.ShouldBeNull();
        motor.Recebido.PlayerName.ShouldBe("Steve");
    }

    [Fact]
    public async Task Cancelar_o_login_nao_cai_para_o_modo_offline()
    {
        // Fechar a janela é uma decisão do jogador, não uma falha de rede: abrir
        // offline aqui seria ignorar o que ele acabou de fazer.
        var result = await Build(
                account: AuthResult.Cancelled(),
                cache: new CacheFalso(new PlayerProfile("Steve", "uuid-do-steve")))
            .HandleAsync(Instalada(), Config(), null, Ct);

        result.Started.ShouldBeFalse();
        result.Message!.ShouldContain("cancelada");
    }

    [Fact]
    public async Task Token_bom_com_perfil_inalcancavel_ainda_joga_online()
    {
        // Recusar por causa de um nome que já sabemos seria perder a partida por
        // um detalhe cosmético.
        var motor = new MotorFalso();

        await Build(
                motor: motor,
                perfis: new PerfilFalso(null),
                cache: new CacheFalso(new PlayerProfile("Steve", "uuid-do-steve")))
            .HandleAsync(Instalada(), Config(), null, Ct);

        motor.Recebido!.AccessToken.ShouldBe("token-do-minecraft");
        motor.Recebido.PlayerName.ShouldBe("Steve");
    }

    [Fact]
    public async Task A_conta_e_verificada_antes_de_baixar_o_java()
    {
        // Descobrir que a sessão expirou depois de cinquenta megabytes de
        // download seria fazer o jogador esperar para só então pedir que entre.
        var java = new FakeJavaLocator();

        await Build(java: java, account: AuthResult.NoStoredCredentials()).HandleAsync(
            Instalada(), Config(), null, Ct);

        java.Requested.ShouldBeNull();
    }

    [Fact]
    public async Task Falha_ao_preparar_o_java_carrega_a_causa_real()
    {
        // "Não foi possível abrir o jogo" não diz se adianta tentar de novo; o
        // texto do erro original diz.
        var java = new FakeJavaLocator { Error = new InvalidOperationException("checksum não confere") };

        var result = await Build(java: java).HandleAsync(
            Instalada(), Config(), null, Ct);

        result.Started.ShouldBeFalse();
        result.Message!.ShouldContain("checksum não confere");
    }

    [Fact]
    public async Task Com_jogo_aberto_recusa_abrir_outro()
    {
        // Duas cópias na mesma pasta escrevem o mesmo mundo ao mesmo tempo e
        // corrompem-no — e quem clicou duas vezes não faz ideia de que foi isso.
        var session = new GameSession();
        var motor = new MotorFalso();
        var caso = Build(motor: motor, session: session);

        await caso.HandleAsync(Instalada(), Config(), null, Ct);

        var segundo = await caso.HandleAsync(Instalada(), Config(), null, Ct);

        segundo.Started.ShouldBeFalse();
        segundo.Message!.ShouldContain("já está aberto");
    }

    [Fact]
    public async Task Abrir_com_sucesso_deixa_a_sessao_a_correr()
    {
        var session = new GameSession();

        await Build(session: session).HandleAsync(Instalada(), Config(), null, Ct);

        session.IsRunning.ShouldBeTrue();
        session.Running!.Manifest.ModpackName.ShouldBe("Pack de Teste");
    }

    [Fact]
    public async Task O_token_do_minecraft_e_readquirido_a_cada_abertura()
    {
        // Ele vale cerca de uma hora e NÃO é guardado: guardá-lo trocaria
        // "expira sozinho" por "fica no disco à espera de quem o leia".
        var autenticador = new ContaFalsa(AuthResult.Success("token-do-minecraft"));

        // Uma sessão nova por chamada: este teste é sobre o token, e reusar a
        // mesma faria o segundo arranque ser recusado pelo guard do jogo aberto.
        await Build(autenticador).HandleAsync(Instalada(), Config(), null, Ct);
        await Build(autenticador).HandleAsync(Instalada(), Config(), null, Ct);

        autenticador.Tentativas.ShouldBe(2);
    }

    // ---------- entrar num servidor ----------

    [Fact]
    public async Task Entrar_na_mesma_versao_abre_direto_no_servidor()
    {
        await using var entrada = new Entrada();
        var server = entrada.Servidor();
        var key = entrada.InstanciaDoServidor(server, "1.0.0");

        var result = await entrada.JoinAsync(server);

        result.Launch.Started.ShouldBeTrue(result.Launch.Message);
        entrada.Motor.Recebido!.Server.ShouldBe(new ServerAddress("mc.exemplo", 25570));
        entrada.Installer.Chamadas.ShouldBe(0);
        result.Instance!.Key.ShouldBe(key);
    }

    [Fact]
    public async Task A_primeira_entrada_instala_uma_instancia_do_servidor()
    {
        await using var entrada = new Entrada();
        var server = entrada.Servidor();

        var result = await entrada.JoinAsync(server);

        result.Launch.Started.ShouldBeTrue(result.Launch.Message);
        entrada.Installer.Chamadas.ShouldBe(1);
        entrada.Installer.Alvo.ShouldBeNull(); // nova, nunca por cima de outra

        var stored = entrada.Store.Manifests[result.Instance!.Key];
        stored.ServerId.ShouldBe(server.Id);
        stored.ServerName.ShouldBe("Survival");
        stored.ModpackVersionId.ShouldBe(server.ModpackVersionId);
    }

    [Fact]
    public async Task Instancia_pessoal_do_mesmo_pack_nunca_entra_no_servidor()
    {
        // A pessoal é do jogador: a versão é a que ele escolheu, e alinhá-la ao
        // servidor desceria ou subiria um mundo que não é do servidor.
        await using var entrada = new Entrada();
        var server = entrada.Servidor();
        var pessoal = InstanceKey.New();
        entrada.Store.Manifests[pessoal] = entrada.Manifesto(server.ModpackVersionId, "1.0.0");

        var result = await entrada.JoinAsync(server);

        result.Instance!.Key.ShouldNotBe(pessoal);
        entrada.Installer.Chamadas.ShouldBe(1);
        entrada.Installer.Alvo.ShouldBeNull();
        entrada.Store.Manifests[pessoal].IsServerInstance.ShouldBeFalse();
    }

    [Fact]
    public async Task Servidor_a_frente_atualiza_a_mesma_instancia()
    {
        await using var entrada = new Entrada();
        var server = entrada.Servidor("1.1.0");
        var key = entrada.InstanciaDoServidor(server, "1.0.0");

        var result = await entrada.JoinAsync(server);

        result.Launch.Started.ShouldBeTrue(result.Launch.Message);
        entrada.Installer.Alvo.ShouldBe(key);
        result.Instance!.Manifest.Version.ShouldBe("1.1.0");
        result.Warning.ShouldBeNull();
    }

    [Fact]
    public async Task Servidor_atras_desce_a_instancia_em_vez_de_recusar()
    {
        // O mundo é do servidor: a pasta local só tem mods e configs, e precisa
        // de casar com os dele para o jogo aceitar entrar.
        await using var entrada = new Entrada();
        var server = entrada.Servidor("0.9.0");
        var key = entrada.InstanciaDoServidor(server, "1.0.0");

        var result = await entrada.JoinAsync(server);

        result.Launch.Started.ShouldBeTrue(result.Launch.Message);
        entrada.Installer.Alvo.ShouldBe(key);
        result.Instance!.Manifest.Version.ShouldBe("0.9.0");
        result.Warning.ShouldBeNull();
    }

    [Fact]
    public async Task Descer_com_mundo_solo_na_instancia_avisa_e_segue()
    {
        await using var entrada = new Entrada { TemMundo = true };
        var server = entrada.Servidor("0.9.0");
        entrada.InstanciaDoServidor(server, "1.0.0");

        var result = await entrada.JoinAsync(server);

        result.Launch.Started.ShouldBeTrue(result.Launch.Message);
        result.Warning!.ShouldContain("v0.9.0");
    }

    [Fact]
    public async Task Servidor_que_trocou_de_pack_libera_a_instancia_antiga()
    {
        // Outro pack é outro Minecraft: a pasta não serve de base. Mas pode ter
        // um mundo solo, e por isso vira pessoal em vez de sumir.
        await using var entrada = new Entrada();
        var server = entrada.Servidor();
        var antiga = InstanceKey.New();
        entrada.Store.Manifests[antiga] = entrada.Manifesto(Guid.CreateVersion7(), "3.0.0") with
        {
            ModpackId = Guid.CreateVersion7(), ServerId = server.Id, ServerName = server.Name
        };

        var result = await entrada.JoinAsync(server);

        result.Instance!.Key.ShouldNotBe(antiga);
        entrada.Store.Manifests[antiga].ServerId.ShouldBeNull();
        entrada.Store.Manifests[result.Instance.Key].ServerId.ShouldBe(server.Id);
    }

    [Fact]
    public async Task Servidor_renomeado_atualiza_o_nome_na_instancia()
    {
        await using var entrada = new Entrada();
        var server = entrada.Servidor();
        var key = entrada.InstanciaDoServidor(server, "1.0.0");

        await entrada.JoinAsync(server with { Name = "Survival 2" });

        entrada.Store.Manifests[key].ServerName.ShouldBe("Survival 2");
    }

    [Fact]
    public async Task Entrar_torna_a_instancia_do_servidor_a_ativa()
    {
        // O "Jogar" seguinte tem de abrir a mesma pasta em que se acabou de entrar.
        await using var entrada = new Entrada();
        var server = entrada.Servidor();

        var result = await entrada.JoinAsync(server);

        entrada.Active.Chosen.ShouldBe(result.Instance!.Key);
    }

    [Fact]
    public async Task Entrada_que_falha_nao_troca_a_ativa()
    {
        await using var entrada = new Entrada();
        entrada.Motor.Falha = true;

        var result = await entrada.JoinAsync(entrada.Servidor());

        result.Launch.Started.ShouldBeFalse();
        entrada.Active.Chosen.ShouldBeNull();
    }

    [Fact]
    public async Task Instalar_prepara_a_pasta_mesmo_com_o_servidor_desligado_e_nao_abre_o_jogo()
    {
        // Deixar tudo pronto enquanto o servidor está fora é o que poupa a
        // espera quando ele voltar.
        await using var entrada = new Entrada();
        var server = entrada.Servidor() with { Status = GameServerStatus.Stopped, ConnectAddress = null };

        var result = await entrada.PrepareAsync(server);

        result.Succeeded.ShouldBeTrue(result.Error);
        entrada.Store.Manifests[result.Instance!.Key].ServerId.ShouldBe(server.Id);
        entrada.Motor.Recebido.ShouldBeNull();
    }

    [Fact]
    public async Task Em_casa_o_jogo_abre_apontando_para_a_rede_local()
    {
        var local = new ServerAddress("192.168.1.100", 25570);
        await using var entrada = new Entrada(new FakeEndpointProbe(local));
        var server = entrada.Servidor() with { LocalAddress = "192.168.1.100:25570" };
        entrada.InstanciaDoServidor(server, "1.0.0");

        var result = await entrada.JoinAsync(server);

        result.Launch.Started.ShouldBeTrue(result.Launch.Message);
        entrada.Motor.Recebido!.Server.ShouldBe(local);
    }

    [Fact]
    public async Task Pelo_tunel_o_jogo_abre_na_porta_local_e_o_tunel_vive_com_ele()
    {
        var tunnel = new FakeGameTunnel();
        await using var entrada = new Entrada(tunnel: tunnel);
        var server = entrada.Servidor() with
        {
            ProbeAddress = "mc.exemplo:25570", TunnelAddress = "survival-tunel.exemplo.com"
        };
        entrada.InstanciaDoServidor(server, "1.0.0");

        var result = await entrada.JoinAsync(server);

        result.Launch.Started.ShouldBeTrue(result.Launch.Message);
        entrada.Motor.Recebido!.Server.ShouldBe(FakeGameTunnel.Local);
        tunnel.Opened.ShouldBe(["survival-tunel.exemplo.com"]);

        // A sessão segura o processo COM o túnel: é ao fim da leitura dele que
        // o túnel fecha. Embrulhado depois, ele ficava aberto para sempre.
        entrada.Session.UsesTunnel.ShouldBeTrue();

        // Fechado só quando o jogo fecha — antes disso, o jogo perderia a conexão.
        tunnel.Last!.Closes.ShouldBe(0);
        result.Launch.Process!.Dispose();
        tunnel.Last.Closes.ShouldBe(1);
    }

    [Fact]
    public async Task Tunel_fora_do_ar_tenta_o_endereco_publicado()
    {
        await using var entrada = new Entrada(tunnel: new FakeGameTunnel(answers: false));
        var server = entrada.Servidor() with
        {
            ProbeAddress = "mc.exemplo:25570", TunnelAddress = "survival-tunel.exemplo.com"
        };
        entrada.InstanciaDoServidor(server, "1.0.0");

        var result = await entrada.JoinAsync(server);

        result.Launch.Started.ShouldBeTrue(result.Launch.Message);
        entrada.Motor.Recebido!.Server.ShouldBe(new ServerAddress("mc.exemplo", 25570));
    }

    [Fact]
    public async Task Servidor_sem_endereco_nenhum_explica_em_vez_de_abrir()
    {
        await using var entrada = new Entrada();

        var result = await entrada.JoinAsync(entrada.Servidor() with { ConnectAddress = null });

        result.Launch.Started.ShouldBeFalse();
        result.Launch.Message!.ShouldContain("endereço");
        entrada.Motor.Recebido.ShouldBeNull();
        entrada.Installer.Chamadas.ShouldBe(0);
    }

    [Fact]
    public async Task Sem_acesso_nao_abre_o_jogo_nem_instala()
    {
        await using var entrada = new Entrada();

        var result = await entrada.JoinAsync(
            entrada.Servidor() with { AccessState = ServerAccessState.None, ConnectAddress = null });

        result.Launch.Started.ShouldBeFalse();
        entrada.Motor.Recebido.ShouldBeNull();
        entrada.Installer.Chamadas.ShouldBe(0);
    }

    /// <summary>
    ///     Um pack, as versões publicadas dele e o disco do jogador — o que o
    ///     "Entrar" consulta. O instalador falso grava no mesmo disco, como o
    ///     real, para a marca de servidor ser verificada onde ela mora.
    /// </summary>
    private sealed class Entrada : IAsyncDisposable
    {
        private readonly FakeServerConnection _connection = new();
        private readonly FakeEndpointProbe _probe;
        private readonly FakeGameTunnel _tunnel;

        public Entrada(FakeEndpointProbe? probe = null, FakeGameTunnel? tunnel = null)
        {
            _probe = probe ?? new FakeEndpointProbe();
            _tunnel = tunnel ?? new FakeGameTunnel();
            Installer = new InstaladorFalso(_connection, Store);
        }

        public ModpackDto Pack { get; } = new()
        {
            Id = Guid.CreateVersion7(),
            Slug = "pack",
            Name = "Pack de Teste",
            MinecraftVersion = "1.21.1",
            Loader = ModLoader.NeoForge
        };

        public FakeInstanceStore Store { get; } = new();

        public MotorFalso Motor { get; } = new();

        public InstaladorFalso Installer { get; }

        public bool TemMundo { get; init; }

        /// <summary>Um servidor do pack, na versão pedida (publicada no catálogo falso).</summary>
        public GameServerDto Servidor(string version = "1.0.0")
        {
            var published = Versao(Pack.Id, version);
            _connection.Versions[published.Id] = published;

            return new GameServerDto
            {
                Id = Guid.CreateVersion7(),
                Name = "Survival",
                ModpackId = Pack.Id,
                ModpackVersionId = published.Id,
                ConnectAddress = "mc.exemplo:25570",
                Status = GameServerStatus.Running,
                Role = ServerRoleDto.Member,
                AccessState = ServerAccessState.Granted
            };
        }

        /// <summary>
        ///     A instância deste servidor já no disco, na versão dada. Na mesma
        ///     do servidor quando o número bate, noutra quando não — é o que faz
        ///     o "Entrar" ter ou não de alinhar.
        /// </summary>
        public InstanceKey InstanciaDoServidor(GameServerDto server, string version)
        {
            var current = _connection.Versions[server.ModpackVersionId];
            var versionId = current.Version == version ? current.Id : Guid.CreateVersion7();

            var key = InstanceKey.New();
            Store.Manifests[key] = Manifesto(versionId, version) with
            {
                ServerId = server.Id, ServerName = server.Name
            };

            return key;
        }

        public InstanceManifest Manifesto(Guid versionId, string version) =>
            LaunchGameTests.Manifesto() with
            {
                ModpackId = Pack.Id, ModpackVersionId = versionId, Version = version
            };

        public ValueTask DisposeAsync() => _connection.DisposeAsync();

        public FakeActiveInstanceStore Active { get; } = new();

        public GameSession Session { get; } = new();

        public Task<JoinServerResult> JoinAsync(GameServerDto server) =>
            new JoinServer(Prepare(), Active, Build(motor: Motor, session: Session), _probe, _tunnel)
                .HandleAsync(Pack, server, Config(), null, Ct);

        /// <summary>Só a preparação — o "Instalar" da aba Servidores.</summary>
        public Task<ServerInstanceResult> PrepareAsync(GameServerDto server) =>
            Prepare().HandleAsync(Config().ServerUrl, Pack, server, null, Ct);

        private PrepareServerInstance Prepare() =>
            new(_connection, Store, Installer, new MundoFalso(TemMundo));
    }

    // ---------- apoio ----------

    private static LaunchGame Build(
        ContaFalsa? autenticador = null,
        FakeJavaLocator? java = null,
        MotorFalso? motor = null,
        AuthResult? account = null,
        GameSession? session = null,
        int? javaDeclarado = null,
        PerfilFalso? perfis = null,
        CacheFalso? cache = null) =>
        new(autenticador ?? new ContaFalsa(account ?? AuthResult.Success("token-do-minecraft")),
            perfis ?? new PerfilFalso(new PlayerProfile("Jogador", "abc123")),
            cache ?? new CacheFalso(),
            java ?? new FakeJavaLocator(),
            new ExigenciaFalsa(javaDeclarado),
            motor ?? new MotorFalso(),
            session ?? new GameSession());

    private static LauncherConfig Config() => new()
    {
        Schema = 1, ServerUrl = new Uri("https://servidor.exemplo/"), AzureClientId = "client"
    };

    private static InstanceManifest Manifesto(string minecraft = "1.21.1") => new()
    {
        Schema = 2,
        ModpackId = Guid.CreateVersion7(),
        ModpackVersionId = Guid.CreateVersion7(),
        ModpackName = "Pack de Teste",
        Version = "1.0.0",
        InstalledAt = DateTimeOffset.UtcNow,
        ManagedFiles = new Dictionary<string, string>(),
        MinecraftVersion = minecraft,
        Loader = ModLoader.NeoForge,
        LoaderVersion = "21.1.0"
    };

    private static InstalledInstance Instalada(string minecraft = "1.21.1") => Instalada(Manifesto(minecraft));

    private static InstalledInstance Instalada(InstanceManifest manifest) =>
        new(InstanceKey.New(),
            manifest,
            0,
            "/instancias/teste");

    private static ModpackVersionDto Versao(Guid modpackId, string version) => new()
    {
        Id = Guid.CreateVersion7(),
        ModpackId = modpackId,
        Version = version,
        LoaderVersion = "21.1.0",
        State = ModpackVersionState.Ready,
        PublishedAt = DateTimeOffset.UtcNow,
        RecommendedMemoryMb = 4096,
        Files = []
    };

    private sealed class MundoFalso(bool temMundo) : IWorldBackup
    {
        public bool HasWorld(InstanceKey key) => temMundo;

        public Task<string> CreateAsync(InstanceKey key, CancellationToken ct) => Task.FromResult("");
    }

    /// <summary>
    ///     Instala como o real no que importa aqui: chave nova quando não há
    ///     alvo, o número da versão pedida no manifesto, e o dono preservado.
    /// </summary>
    private sealed class InstaladorFalso(FakeServerConnection connection, FakeInstanceStore store)
        : IInstanceInstaller
    {
        public int Chamadas { get; private set; }

        public InstanceKey? Alvo { get; private set; }

        public async Task<InstallResult> HandleAsync(
            Uri serverUrl,
            ModpackDto modpack,
            Guid versionId,
            InstanceKey? target,
            IProgress<InstallProgress>? progress,
            CancellationToken ct)
        {
            Chamadas++;
            Alvo = target;

            var key = target ?? InstanceKey.New();
            var local = await store.ReadManifestAsync(key, ct);
            var version = await connection.GetModpackVersionAsync(versionId, ct);

            var manifest = Manifesto() with
            {
                ModpackId = modpack.Id,
                ModpackVersionId = versionId,
                Version = version.Version,
                ServerId = local?.ServerId,
                ServerName = local?.ServerName
            };

            await store.WriteManifestAsync(key, manifest, ct);
            return InstallResult.Success(key, manifest);
        }
    }

    private sealed class ContaFalsa(AuthResult result) : IMinecraftAuthenticator
    {
        public int Tentativas { get; private set; }

        public Task<AuthResult> TrySilentAsync(string azureClientId, CancellationToken ct)
        {
            Tentativas++;
            return Task.FromResult(result);
        }

        public Task<AuthResult> SignInAsync(string azureClientId, CancellationToken ct) =>
            Task.FromResult(result);

        public Task SignOutAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class PerfilFalso(PlayerProfile? perfil) : IPlayerProfileSource
    {
        public Task<PlayerProfile?> GetAsync(string accessToken, CancellationToken ct) =>
            Task.FromResult(perfil);
    }

    private sealed class CacheFalso(PlayerProfile? inicial = null) : IPlayerProfileCache
    {
        public PlayerProfile? Guardado { get; private set; } = inicial;

        public Task<PlayerProfile?> ReadAsync(CancellationToken ct) => Task.FromResult(Guardado);

        public Task WriteAsync(PlayerProfile profile, CancellationToken ct)
        {
            Guardado = profile;
            return Task.CompletedTask;
        }
    }

    private sealed class ExigenciaFalsa(int? declarado) : IJavaRequirementSource
    {
        public Task<int?> GetRequiredJavaAsync(string minecraftVersion, CancellationToken ct) =>
            Task.FromResult(declarado);
    }

    private sealed class MotorFalso : IGameLauncher
    {
        public GameLaunchRequest? Recebido { get; private set; }

        /// <summary>O jogo não abre (Java quebrado, por exemplo).</summary>
        public bool Falha { get; set; }

        public Task<GameLaunchResult> LaunchAsync(
            GameLaunchRequest request,
            IProgress<GameLaunchProgress>? progress,
            CancellationToken ct)
        {
            Recebido = request;
            return Task.FromResult(Falha
                ? GameLaunchResult.Failed("o jogo não abriu")
                : GameLaunchResult.Ok(new FakeGameProcess()));
        }
    }
}
