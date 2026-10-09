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
        var instance = Instalada();
        var motor = new MotorFalso();
        var join = Join(motor, out var installer);

        var result = await join.HandleAsync(instance, Servidor(instance.Manifest), Config(), null, Ct);

        result.Launch.Started.ShouldBeTrue(result.Launch.Message);
        motor.Recebido!.Server.ShouldBe(new ServerAddress("mc.exemplo", 25570));
        installer.Chamadas.ShouldBe(0);
        result.Updated.ShouldBeNull();
    }

    [Fact]
    public async Task Servidor_a_frente_atualiza_a_instancia_e_entra()
    {
        var instance = Instalada();
        var nova = Versao(instance.Manifest.ModpackId, "1.1.0");
        var motor = new MotorFalso();
        var join = Join(motor, out var installer, nova);

        var result = await join.HandleAsync(
            instance, Servidor(instance.Manifest) with { ModpackVersionId = nova.Id }, Config(), null, Ct);

        result.Launch.Started.ShouldBeTrue(result.Launch.Message);

        // A MESMA instância, para o mundo ir junto — nunca uma nova ao lado.
        installer.Alvo.ShouldBe(instance.Key);
        installer.Versao.ShouldBe(nova.Id);
        result.Updated!.Manifest.Version.ShouldBe("1.1.0");
        motor.Recebido!.Server.ShouldNotBeNull();
    }

    [Fact]
    public async Task Servidor_atras_recusa_sem_tocar_na_instancia()
    {
        var instance = Instalada();
        var antiga = Versao(instance.Manifest.ModpackId, "0.9.0");
        var motor = new MotorFalso();
        var join = Join(motor, out var installer, antiga);

        var result = await join.HandleAsync(
            instance, Servidor(instance.Manifest) with { ModpackVersionId = antiga.Id }, Config(), null, Ct);

        result.Launch.Started.ShouldBeFalse();
        result.Launch.Message!.ShouldContain("v0.9.0");
        installer.Chamadas.ShouldBe(0);
        motor.Recebido.ShouldBeNull();
    }

    [Fact]
    public async Task Sem_acesso_nao_abre_o_jogo()
    {
        var instance = Instalada();
        var motor = new MotorFalso();
        var join = Join(motor, out _);

        var result = await join.HandleAsync(
            instance,
            Servidor(instance.Manifest) with { AccessState = ServerAccessState.None, ConnectAddress = null },
            Config(), null, Ct);

        result.Launch.Started.ShouldBeFalse();
        motor.Recebido.ShouldBeNull();
    }

    private static JoinServer Join(MotorFalso motor, out InstaladorFalso installer, params ModpackVersionDto[] versions)
    {
        var connection = new FakeServerConnection();
        foreach (var v in versions)
            connection.Versions[v.Id] = v;

        var instalador = new InstaladorFalso(versions);
        installer = instalador;

        return new JoinServer(
            connection,
            new UpdateInstance(instalador, new SemMundo()),
            Build(motor: motor));
    }

    private static GameServerDto Servidor(InstanceManifest manifest) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = "Survival",
        ModpackId = manifest.ModpackId,
        ModpackVersionId = manifest.ModpackVersionId,
        ConnectAddress = "mc.exemplo:25570",
        Status = GameServerStatus.Running,
        Role = ServerRoleDto.Member,
        AccessState = ServerAccessState.Granted
    };

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

    private sealed class SemMundo : IWorldBackup
    {
        public bool HasWorld(InstanceKey key) => false;

        public Task<string> CreateAsync(InstanceKey key, CancellationToken ct) => Task.FromResult("");
    }

    /// <summary>Instala gravando o número da versão pedida no manifesto, como o real.</summary>
    private sealed class InstaladorFalso(IReadOnlyList<ModpackVersionDto> versions) : IInstanceInstaller
    {
        public int Chamadas { get; private set; }

        public InstanceKey? Alvo { get; private set; }

        public Guid? Versao { get; private set; }

        public Task<InstallResult> HandleAsync(
            Uri serverUrl,
            ModpackDto modpack,
            Guid versionId,
            InstanceKey? target,
            IProgress<InstallProgress>? progress,
            CancellationToken ct)
        {
            Chamadas++;
            Alvo = target;
            Versao = versionId;

            var manifest = Manifesto() with
            {
                ModpackVersionId = versionId, Version = versions.First(v => v.Id == versionId).Version
            };

            return Task.FromResult(InstallResult.Success(target!.Value, manifest));
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

        public Task<GameLaunchResult> LaunchAsync(
            GameLaunchRequest request,
            IProgress<GameLaunchProgress>? progress,
            CancellationToken ct)
        {
            Recebido = request;
            return Task.FromResult(GameLaunchResult.Ok(new FakeGameProcess()));
        }
    }
}
