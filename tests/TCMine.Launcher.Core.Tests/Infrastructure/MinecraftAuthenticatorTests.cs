using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Tests.Fakes;
using TCMine.Launcher.Infrastructure.Identity;

namespace TCMine.Launcher.Core.Tests.Infrastructure;

/// <summary>
///     A cadeia Xbox Live → XSTS → Minecraft.
///     Vale a pena testá-la a sério porque é a parte da autenticação que NÃO
///     depende de app do Azure nem de broker: dado um token da Microsoft, tudo o
///     que acontece daí em diante é HTTP com formato fixo. Cada detalhe abaixo já
///     é um modo de falhar conhecido destas APIs — e todos falham com mensagens
///     que não dizem o que está errado.
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
        var auth = Montar(handler, AuthResult.Success("token-da-microsoft"));

        var resultado = await auth.TrySilentAsync("client", Ct);

        resultado.Outcome.ShouldBe(AuthOutcome.Success);
        resultado.AccessToken.ShouldBe("token-do-minecraft");
    }

    [Fact]
    public async Task O_ticket_do_xbox_live_leva_o_prefixo_d()
    {
        // Sem o "d=", o Xbox Live responde 400 com um token perfeitamente válido.
        // É o erro mais fácil de cometer e o mais difícil de diagnosticar.
        var handler = CadeiaFeliz();

        await Montar(handler, AuthResult.Success("token-da-microsoft")).TrySilentAsync("client", Ct);

        handler.Pedidos[0].Body.ShouldContain("\"RpsTicket\":\"d=token-da-microsoft\"");
    }

    [Fact]
    public async Task O_pedido_ao_xsts_nao_leva_campos_do_salto_anterior()
    {
        // O Xbox Live recusa propriedade desconhecida sem dizer qual. Os campos
        // que não valem para o pedido têm de sair do JSON, não ir nulos.
        var handler = CadeiaFeliz();

        await Montar(handler, AuthResult.Success("token-da-microsoft")).TrySilentAsync("client", Ct);

        var pedidoAoXsts = handler.Pedidos[1].Body;

        pedidoAoXsts.ShouldNotContain("RpsTicket");
        pedidoAoXsts.ShouldNotContain("AuthMethod");
        pedidoAoXsts.ShouldContain("\"UserTokens\":[\"token-do-xbox\"]");
    }

    [Fact]
    public async Task O_minecraft_recebe_o_user_hash_do_xsts_com_o_token_do_xsts()
    {
        // O par tem de ser o do MESMO salto. O Xbox Live devolve um user hash do
        // mesmo jogador, e usá-lo aqui compila, parece certo e é recusado.
        var handler = CadeiaFeliz();

        await Montar(handler, AuthResult.Success("token-da-microsoft")).TrySilentAsync("client", Ct);

        handler.Pedidos[2].Body.ShouldContain("XBL3.0 x=hash-do-xsts;token-do-xsts-final");
    }

    [Fact]
    public async Task Conta_sem_perfil_do_xbox_explica_o_que_fazer()
    {
        var handler = new FakeHttpHandler()
            .Responde(XboxLive, HttpStatusCode.OK, RespostaDoXboxLive())
            .Responde(Xsts, HttpStatusCode.Unauthorized, new { XErr = 2148916233L });

        var resultado = await Montar(handler, AuthResult.Success("token-da-microsoft"))
            .SignInAsync("client", Ct);

        resultado.Outcome.ShouldBe(AuthOutcome.Failed);

        // A mensagem é o produto deste caso: sem ela o jogador vê "não foi
        // possível entrar" e não tem como saber que precisa criar um perfil.
        resultado.Message!.ShouldContain("xbox.com");
    }

    [Fact]
    public async Task Conta_de_menor_recebe_a_mensagem_da_familia()
    {
        var handler = new FakeHttpHandler()
            .Responde(XboxLive, HttpStatusCode.OK, RespostaDoXboxLive())
            .Responde(Xsts, HttpStatusCode.Unauthorized, new { XErr = 2148916238L });

        var resultado = await Montar(handler, AuthResult.Success("token-da-microsoft"))
            .SignInAsync("client", Ct);

        resultado.Message!.ShouldContain("família");
    }

    [Fact]
    public async Task Sem_credencial_guardada_a_cadeia_nem_comeca()
    {
        // O desfecho tem de chegar intacto à tela: reembrulhá-lo como falha faria
        // o primeiro arranque de toda instalação mostrar um erro.
        var handler = new FakeHttpHandler();

        var resultado = await Montar(handler, AuthResult.NoStoredCredentials()).TrySilentAsync("client", Ct);

        resultado.Outcome.ShouldBe(AuthOutcome.NoStoredCredentials);
        handler.Pedidos.ShouldBeEmpty();
    }

    [Fact]
    public async Task Desistir_do_login_nao_vira_erro()
    {
        var handler = new FakeHttpHandler();

        var resultado = await Montar(handler, AuthResult.Cancelled()).SignInAsync("client", Ct);

        resultado.Outcome.ShouldBe(AuthOutcome.Cancelled);
        handler.Pedidos.ShouldBeEmpty();
    }

    [Fact]
    public async Task Build_sem_msal_diz_que_nao_sabe_entrar()
    {
        // O desfecho e a mensagem do provedor atravessam a cadeia sem tradução.
        var handler = new FakeHttpHandler();

        var resultado = await Montar(handler, AuthResult.Unavailable("build sem login"))
            .SignInAsync("client", Ct);

        resultado.Outcome.ShouldBe(AuthOutcome.Unavailable);
        resultado.Message.ShouldBe("build sem login");
    }

    [Fact]
    public async Task Resposta_sem_token_falha_em_vez_de_seguir()
    {
        var handler = new FakeHttpHandler()
            .Responde(XboxLive, HttpStatusCode.OK, new { DisplayClaims = new { xui = new[] { new { uhs = "h" } } } });

        var resultado = await Montar(handler, AuthResult.Success("token-da-microsoft"))
            .TrySilentAsync("client", Ct);

        resultado.Outcome.ShouldBe(AuthOutcome.Failed);

        // Não chegou ao XSTS: seguir com token nulo daria NullReference no salto
        // seguinte, e o jogador veria um erro sem relação com a causa.
        handler.Pedidos.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Sair_nao_toca_na_rede()
    {
        // Sair é do provedor: é ele que tem cache. Nada aqui tem o que descartar.
        var provedor = new ProvedorFalso(AuthResult.NoStoredCredentials());
        var handler = new FakeHttpHandler();

        await Montar(handler, provedor).SignOutAsync(Ct);

        provedor.Saiu.ShouldBeTrue();
        handler.Pedidos.ShouldBeEmpty();
    }

    // ---- Montagem ----

    private static FakeHttpHandler CadeiaFeliz() =>
        new FakeHttpHandler()
            .Responde(XboxLive, HttpStatusCode.OK, RespostaDoXboxLive())
            .Responde(Xsts, HttpStatusCode.OK, new
            {
                Token = "token-do-xsts-final",
                DisplayClaims = new { xui = new[] { new { uhs = "hash-do-xsts" } } }
            })
            .Responde(Minecraft, HttpStatusCode.OK, new { access_token = "token-do-minecraft" });

    private static object RespostaDoXboxLive() => new
    {
        Token = "token-do-xbox",
        DisplayClaims = new { xui = new[] { new { uhs = "hash-do-xbox-live" } } }
    };

    private static MinecraftAuthenticator Montar(FakeHttpHandler handler, AuthResult daMicrosoft) =>
        Montar(handler, new ProvedorFalso(daMicrosoft));

    private static MinecraftAuthenticator Montar(FakeHttpHandler handler, ProvedorFalso provedor) =>
        new(new HttpClient(handler), provedor, NullLogger<MinecraftAuthenticator>.Instance);

    private sealed class ProvedorFalso(AuthResult resultado) : IMicrosoftTokenProvider
    {
        public bool Saiu { get; private set; }

        public Task<AuthResult> TrySilentAsync(string azureClientId, CancellationToken ct) =>
            Task.FromResult(resultado);

        public Task<AuthResult> SignInAsync(string azureClientId, CancellationToken ct) =>
            Task.FromResult(resultado);

        public Task SignOutAsync(CancellationToken ct)
        {
            Saiu = true;
            return Task.CompletedTask;
        }
    }
}
