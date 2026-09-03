using TCMine.Contracts;
using TCMine.Contracts.Identity;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Identity;

namespace TCMine.Launcher.Core.Tests.Identity;

/// <summary>
///     Entrar são dois passos que só valem juntos: provar a conta à Microsoft e
///     trocar essa prova por uma sessão no servidor.
///     O que estes testes trancam é o comportamento nas bordas — o arranque sem
///     credencial, a desistência do jogador, a conta recusada e a rede fora —
///     porque é onde a diferença entre "avisar" e "calar" decide se o launcher
///     parece quebrado.
/// </summary>
public class SignInTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static LauncherConfig Config => new()
    {
        Schema = 1, ServerUrl = new Uri("https://servidor.exemplo/"), AzureClientId = "client"
    };

    [Fact]
    public async Task Arranque_sem_credencial_guardada_fica_calado()
    {
        // O primeiro arranque de toda instalação passa por aqui. Uma mensagem de
        // erro na abertura ensinaria o jogador a ignorar mensagens de erro.
        var api = new ApiFalsa();

        var entrada = new SignIn(
            new AutenticadorFalso(MinecraftAuthResult.NoStoredCredentials()), api);

        var estado = await entrada.ResumeAsync(Config, Ct);

        estado.Status.ShouldBe(SignInStatus.SignedOut);
        estado.Message.ShouldBeNull();
        api.TokenRecebido.ShouldBeNull("sem token não há o que trocar com o servidor");
    }

    [Fact]
    public async Task Arranque_com_credencial_guardada_entra_sozinho()
    {
        var api = new ApiFalsa(SessionResult.Success(Sessao()));

        var entrada = new SignIn(new AutenticadorFalso(MinecraftAuthResult.Success("token-mc")), api);

        var estado = await entrada.ResumeAsync(Config, Ct);

        estado.IsSignedIn.ShouldBeTrue();
        estado.Session!.DisplayName.ShouldBe("ana");
        api.TokenRecebido.ShouldBe("token-mc");
    }

    [Fact]
    public async Task Fechar_a_janela_do_navegador_nao_e_erro()
    {
        // Cancelar é uma decisão. Avisar seria repetir ao jogador o que ele
        // acabou de fazer.
        var entrada = new SignIn(new AutenticadorFalso(MinecraftAuthResult.Cancelled()), new ApiFalsa());

        var estado = await entrada.InteractiveAsync(Config, Ct);

        estado.Status.ShouldBe(SignInStatus.SignedOut);
        estado.Message.ShouldBeNull();
    }

    [Fact]
    public async Task Falha_da_microsoft_no_clique_tem_o_que_dizer()
    {
        var entrada = new SignIn(
            new AutenticadorFalso(MinecraftAuthResult.Unavailable("ainda não disponível")),
            new ApiFalsa());

        var estado = await entrada.InteractiveAsync(Config, Ct);

        estado.Status.ShouldBe(SignInStatus.Failed);
        estado.Message.ShouldBe("ainda não disponível");
    }

    [Fact]
    public async Task Conta_recusada_pelo_servidor_e_distinta_de_falha_de_rede()
    {
        // Repetir com a mesma conta dá no mesmo; a interface precisa dizer para
        // trocar de conta, não para tentar de novo.
        var entrada = new SignIn(
            new AutenticadorFalso(MinecraftAuthResult.Success("token-mc")),
            new ApiFalsa(SessionResult.Rejected("conta não reconhecida")));

        var estado = await entrada.InteractiveAsync(Config, Ct);

        estado.Status.ShouldBe(SignInStatus.Rejected);
        estado.Message.ShouldBe("conta não reconhecida");
    }

    [Fact]
    public async Task Servidor_fora_do_ar_no_login_e_falha_e_nao_recusa()
    {
        var entrada = new SignIn(
            new AutenticadorFalso(MinecraftAuthResult.Success("token-mc")),
            new ApiFalsa(SessionResult.Failed("sem rede")));

        var estado = await entrada.InteractiveAsync(Config, Ct);

        estado.Status.ShouldBe(SignInStatus.Failed);
    }

    [Fact]
    public async Task Sair_encerra_a_sessao_no_servidor_antes_da_credencial_local()
    {
        // A ordem é a garantia: na ordem inversa, uma queda de rede no meio
        // deixaria a máquina sem credencial e a sessão viva do outro lado, sem
        // como encerrá-la.
        var ordem = new List<string>();
        var api = new ApiFalsa(registro: () => ordem.Add("servidor"));
        var autenticador = new AutenticadorFalso(
            MinecraftAuthResult.NoStoredCredentials(), registro: () => ordem.Add("local"));

        var estado = await new SignIn(autenticador, api).SignOutAsync(Config, Ct);

        estado.Status.ShouldBe(SignInStatus.SignedOut);
        ordem.ShouldBe(["servidor", "local"]);
    }

    // ---------- apoio ----------

    private static LauncherSessionDto Sessao() => new()
    {
        UserId = Guid.CreateVersion7(), DisplayName = "ana", MinecraftUuid = "abc123"
    };

    private sealed class AutenticadorFalso(MinecraftAuthResult resultado, Action? registro = null)
        : IMinecraftAuthenticator
    {
        public Task<MinecraftAuthResult> TrySilentAsync(string azureClientId, CancellationToken ct) =>
            Task.FromResult(resultado);

        public Task<MinecraftAuthResult> SignInAsync(string azureClientId, CancellationToken ct) =>
            Task.FromResult(resultado);

        public Task SignOutAsync(CancellationToken ct)
        {
            registro?.Invoke();
            return Task.CompletedTask;
        }
    }

    private sealed class ApiFalsa(SessionResult? resultado = null, Action? registro = null) : ILauncherSessionApi
    {
        public string? TokenRecebido { get; private set; }

        public Task<SessionResult> SignInAsync(Uri serverUrl, string minecraftAccessToken, CancellationToken ct)
        {
            TokenRecebido = minecraftAccessToken;

            return Task.FromResult(resultado ?? SessionResult.Failed("sem resposta"));
        }

        public Task SignOutAsync(Uri serverUrl, CancellationToken ct)
        {
            registro?.Invoke();
            return Task.CompletedTask;
        }
    }
}
