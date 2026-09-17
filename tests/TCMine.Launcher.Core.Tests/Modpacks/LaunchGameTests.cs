using TCMine.Contracts;
using TCMine.Contracts.Identity;
using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.Core.Sync;

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

        var resultado = await Montar(motor: motor).HandleAsync(
            Instalada(), Config(), Sessao(), null, Ct);

        resultado.Started.ShouldBeTrue();

        var pedido = motor.Recebido.ShouldNotBeNull();
        pedido.AccessToken.ShouldBe("token-do-minecraft");
        pedido.JavaPath.ShouldBe("/runtimes/21/bin/java");
        pedido.MinecraftVersion.ShouldBe("1.21.1");
        pedido.Loader.ShouldBe(ModLoader.NeoForge);
        pedido.PlayerUuid.ShouldBe("abc123");
        pedido.PlayerName.ShouldBe("Jogador");
    }

    [Fact]
    public async Task O_java_pedido_vem_da_versao_do_minecraft()
    {
        // A regra vive no JavaRequirement; o que este teste trava é que o caso
        // de uso a consulta em vez de assumir um major fixo.
        var java = new JavaFalso();

        await Montar(java: java).HandleAsync(
            Instalada(minecraft: "1.20.4"), Config(), Sessao(), null, Ct);

        java.Pedido.ShouldBe(17);
    }

    [Fact]
    public async Task Instancia_de_schema_antigo_manda_reinstalar()
    {
        // Sem MinecraftVersion não há o que executar, e adivinhar abriria o jogo
        // errado. A mensagem tem de nomear o pack: o jogador pode ter vários.
        var manifesto = Manifesto() with { MinecraftVersion = null, Loader = null };

        var resultado = await Montar().HandleAsync(
            Instalada(manifesto), Config(), Sessao(), null, Ct);

        resultado.Started.ShouldBeFalse();
        resultado.Message!.ShouldContain("Reinstale");
        resultado.Message!.ShouldContain("Pack de Teste");
    }

    [Fact]
    public async Task Sessao_expirada_manda_entrar_de_novo()
    {
        // O desfecho importa: mandar "tente de novo" a quem precisa fazer login
        // faria o jogador clicar para sempre sem nunca resolver.
        var resultado = await Montar(conta: AuthResult.NoStoredCredentials()).HandleAsync(
            Instalada(), Config(), Sessao(), null, Ct);

        resultado.Started.ShouldBeFalse();
        resultado.Message!.ShouldContain("Entre novamente");
    }

    [Fact]
    public async Task A_conta_e_verificada_antes_de_baixar_o_java()
    {
        // Descobrir que a sessão expirou depois de cinquenta megabytes de
        // download seria fazer o jogador esperar para só então pedir que entre.
        var java = new JavaFalso();

        await Montar(java: java, conta: AuthResult.NoStoredCredentials()).HandleAsync(
            Instalada(), Config(), Sessao(), null, Ct);

        java.Pedido.ShouldBeNull();
    }

    [Fact]
    public async Task Falha_ao_preparar_o_java_carrega_a_causa_real()
    {
        // "Não foi possível abrir o jogo" não diz se adianta tentar de novo; o
        // texto do erro original diz.
        var java = new JavaFalso { Erro = new InvalidOperationException("checksum não confere") };

        var resultado = await Montar(java: java).HandleAsync(
            Instalada(), Config(), Sessao(), null, Ct);

        resultado.Started.ShouldBeFalse();
        resultado.Message!.ShouldContain("checksum não confere");
    }

    [Fact]
    public async Task O_token_do_minecraft_e_readquirido_a_cada_abertura()
    {
        // Ele vale cerca de uma hora e NÃO é guardado: guardá-lo trocaria
        // "expira sozinho" por "fica no disco à espera de quem o leia".
        var autenticador = new ContaFalsa(AuthResult.Success("token-do-minecraft"));
        var caso = Montar(autenticador: autenticador);

        await caso.HandleAsync(Instalada(), Config(), Sessao(), null, Ct);
        await caso.HandleAsync(Instalada(), Config(), Sessao(), null, Ct);

        autenticador.Tentativas.ShouldBe(2);
    }

    // ---------- apoio ----------

    private static LaunchGame Montar(
        ContaFalsa? autenticador = null,
        JavaFalso? java = null,
        MotorFalso? motor = null,
        AuthResult? conta = null) =>
        new(autenticador ?? new ContaFalsa(conta ?? AuthResult.Success("token-do-minecraft")),
            java ?? new JavaFalso(),
            motor ?? new MotorFalso());

    private static LauncherConfig Config() => new()
    {
        Schema = 1, ServerUrl = new Uri("https://servidor.exemplo/"), AzureClientId = "client"
    };

    private static LauncherSessionDto Sessao() => new()
    {
        UserId = Guid.CreateVersion7(), DisplayName = "Jogador", MinecraftUuid = "abc123"
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

    private static InstalledInstance Instalada(InstanceManifest manifesto) =>
        new(new InstanceKey(manifesto.ModpackId, manifesto.ModpackVersionId),
            manifesto,
            SizeBytes: 0,
            Path: "/instancias/teste");

    private sealed class ContaFalsa(AuthResult resultado) : IMinecraftAuthenticator
    {
        public int Tentativas { get; private set; }

        public Task<AuthResult> TrySilentAsync(string azureClientId, CancellationToken ct)
        {
            Tentativas++;
            return Task.FromResult(resultado);
        }

        public Task<AuthResult> SignInAsync(string azureClientId, CancellationToken ct) =>
            Task.FromResult(resultado);

        public Task SignOutAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class JavaFalso : IJavaLocator
    {
        public int? Pedido { get; private set; }

        public Exception? Erro { get; init; }

        public Task<string> EnsureRuntimeAsync(int majorVersion, IProgress<double>? progress, CancellationToken ct)
        {
            Pedido = majorVersion;

            return Erro is not null
                ? Task.FromException<string>(Erro)
                : Task.FromResult($"/runtimes/{majorVersion}/bin/java");
        }
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
            return Task.FromResult(GameLaunchResult.Ok());
        }
    }
}
