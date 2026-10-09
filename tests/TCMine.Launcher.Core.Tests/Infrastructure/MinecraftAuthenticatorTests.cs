using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Tests.Fakes;
using TCMine.Launcher.Infrastructure.Identity;
using TCMine.MinecraftAuth;

namespace TCMine.Launcher.Core.Tests.Infrastructure;

/// <summary>
///     O que é do <see cref="MinecraftAuthenticator" />: delegar ao
///     <see cref="IMicrosoftTokenProvider" /> (MSAL) e, só se ele disser sim,
///     ao <see cref="MinecraftTokenExchange" /> compartilhado — e traduzir entre
///     os dois vocabulários de resultado. O detalhe de fio da cadeia Xbox
///     Live → XSTS → Minecraft (prefixo "d=", par de token certo, mensagens de
///     XErr) mudou-se para <c>TCMine.MinecraftAuth.Tests</c>, junto do código
///     que ele testa.
/// </summary>
public class MinecraftAuthenticatorTests
{
    private const string XboxLive = "https://user.auth.xboxlive.com/user/authenticate";
    private const string Xsts = "https://xsts.auth.xboxlive.com/xsts/authorize";
    private const string Minecraft = "https://api.minecraftservices.com/authentication/login_with_xbox";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Cadeia_completa_devolve_o_token_do_minecraft()
    {
        var handler = CadeiaFeliz();
        var auth = Build(handler, AuthResult.Success("token-da-microsoft"));

        var result = await auth.TrySilentAsync("client", Ct);

        result.Outcome.ShouldBe(AuthOutcome.Success);
        result.AccessToken.ShouldBe("token-do-minecraft");
    }

    [Fact]
    public async Task Minecraft_falhou_vira_AuthResult_Failed_com_a_mesma_mensagem()
    {
        var handler = new FakeHttpHandler()
            .Responde(XboxLive, HttpStatusCode.Unauthorized);

        var result = await Build(handler, AuthResult.Success("token-da-microsoft")).TrySilentAsync("client", Ct);

        result.Outcome.ShouldBe(AuthOutcome.Failed);
        result.Message.ShouldNotBeNull().ShouldContain("Xbox Live");
    }

    [Fact]
    public async Task Sem_credencial_guardada_a_cadeia_nem_comeca()
    {
        // O desfecho tem de chegar intacto à tela: reembrulhá-lo como falha faria
        // o primeiro arranque de toda instalação mostrar um erro.
        var handler = new FakeHttpHandler();

        var result = await Build(handler, AuthResult.NoStoredCredentials()).TrySilentAsync("client", Ct);

        result.Outcome.ShouldBe(AuthOutcome.NoStoredCredentials);
        handler.Pedidos.ShouldBeEmpty();
    }

    [Fact]
    public async Task Desistir_do_login_nao_vira_erro()
    {
        var handler = new FakeHttpHandler();

        var result = await Build(handler, AuthResult.Cancelled()).SignInAsync("client", Ct);

        result.Outcome.ShouldBe(AuthOutcome.Cancelled);
        handler.Pedidos.ShouldBeEmpty();
    }

    [Fact]
    public async Task Build_sem_msal_diz_que_nao_sabe_entrar()
    {
        // O desfecho e a mensagem do provedor atravessam a cadeia sem tradução.
        var handler = new FakeHttpHandler();

        var result = await Build(handler, AuthResult.Unavailable("build sem login"))
            .SignInAsync("client", Ct);

        result.Outcome.ShouldBe(AuthOutcome.Unavailable);
        result.Message.ShouldBe("build sem login");
    }

    [Fact]
    public async Task Sair_nao_toca_na_rede()
    {
        // Sair é do provedor: é ele que tem cache. Nada aqui tem o que descartar.
        var provedor = new ProvedorFalso(AuthResult.NoStoredCredentials());
        var handler = new FakeHttpHandler();

        await Build(handler, provedor).SignOutAsync(Ct);

        provedor.Saiu.ShouldBeTrue();
        handler.Pedidos.ShouldBeEmpty();
    }

    // ---- Montagem ----

    private static FakeHttpHandler CadeiaFeliz() =>
        new FakeHttpHandler()
            .Responde(XboxLive, HttpStatusCode.OK, RespostaDoXboxLive())
            .Responde(Xsts, HttpStatusCode.OK,
                new
                {
                    Token = "token-do-xsts-final",
                    DisplayClaims = new { xui = new[] { new { uhs = "hash-do-xsts" } } }
                })
            .Responde(Minecraft, HttpStatusCode.OK, new { access_token = "token-do-minecraft" });

    private static object RespostaDoXboxLive() => new
    {
        Token = "token-do-xbox", DisplayClaims = new { xui = new[] { new { uhs = "hash-do-xbox-live" } } }
    };

    private static MinecraftAuthenticator Build(FakeHttpHandler handler, AuthResult daMicrosoft) =>
        Build(handler, new ProvedorFalso(daMicrosoft));

    private static MinecraftAuthenticator Build(FakeHttpHandler handler, ProvedorFalso provedor) =>
        new(provedor, new MinecraftTokenExchange(new HttpClient(handler), NullLogger<MinecraftTokenExchange>.Instance));

    private sealed class ProvedorFalso(AuthResult result) : IMicrosoftTokenProvider
    {
        public bool Saiu { get; private set; }

        public Task<AuthResult> TrySilentAsync(string azureClientId, CancellationToken ct) =>
            Task.FromResult(result);

        public Task<AuthResult> SignInAsync(string azureClientId, CancellationToken ct) =>
            Task.FromResult(result);

        public Task SignOutAsync(CancellationToken ct)
        {
            Saiu = true;
            return Task.CompletedTask;
        }
    }
}
