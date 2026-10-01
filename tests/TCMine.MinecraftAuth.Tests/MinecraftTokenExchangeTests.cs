using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using TCMine.MinecraftAuth.Tests.Fakes;

namespace TCMine.MinecraftAuth.Tests;

/// <summary>
///     A cadeia Xbox Live → XSTS → Minecraft.
///     Vale a pena testá-la a sério porque é a parte da autenticação que NÃO
///     depende de app do Azure nem de broker nem de redirect de navegador: dado
///     um token da Microsoft, tudo o que acontece daí em diante é HTTP com
///     formato fixo — o mesmo para o launcher e para o painel web. Cada detalhe
///     abaixo já é um modo de falhar conhecido destas APIs — e todos falham com
///     mensagens que não dizem o que está errado.
/// </summary>
public class MinecraftTokenExchangeTests
{
    private const string XboxLive = "https://user.auth.xboxlive.com/user/authenticate";
    private const string Xsts = "https://xsts.auth.xboxlive.com/xsts/authorize";
    private const string Minecraft = "https://api.minecraftservices.com/authentication/login_with_xbox";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Cadeia_completa_devolve_o_token_do_minecraft()
    {
        var handler = CadeiaFeliz();

        var result = await Build(handler).ExchangeAsync("token-da-microsoft", Ct);

        result.Outcome.ShouldBe(MinecraftTokenOutcome.Success);
        result.AccessToken.ShouldBe("token-do-minecraft");
    }

    [Fact]
    public async Task O_ticket_do_xbox_live_leva_o_prefixo_d()
    {
        // Sem o "d=", o Xbox Live responde 400 com um token perfeitamente válido.
        // É o erro mais fácil de cometer e o mais difícil de diagnosticar.
        var handler = CadeiaFeliz();

        await Build(handler).ExchangeAsync("token-da-microsoft", Ct);

        handler.Pedidos[0].Body.ShouldContain("\"RpsTicket\":\"d=token-da-microsoft\"");
    }

    [Fact]
    public async Task O_pedido_ao_xsts_nao_leva_campos_do_salto_anterior()
    {
        // O Xbox Live recusa propriedade desconhecida sem dizer qual. Os campos
        // que não valem para o pedido têm de sair do JSON, não ir nulos.
        var handler = CadeiaFeliz();

        await Build(handler).ExchangeAsync("token-da-microsoft", Ct);

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

        await Build(handler).ExchangeAsync("token-da-microsoft", Ct);

        handler.Pedidos[2].Body.ShouldContain("XBL3.0 x=hash-do-xsts;token-do-xsts-final");
    }

    [Fact]
    public async Task Conta_sem_perfil_do_xbox_explica_o_que_fazer()
    {
        var handler = new FakeHttpHandler()
            .Responde(XboxLive, HttpStatusCode.OK, RespostaDoXboxLive())
            .Responde(Xsts, HttpStatusCode.Unauthorized, new { XErr = 2148916233L });

        var result = await Build(handler).ExchangeAsync("token-da-microsoft", Ct);

        result.Outcome.ShouldBe(MinecraftTokenOutcome.Failed);

        // A mensagem é o produto deste caso: sem ela quem está autenticando vê
        // "não foi possível entrar" e não tem como saber que precisa criar um perfil.
        result.Message!.ShouldContain("xbox.com");
    }

    [Fact]
    public async Task Conta_de_menor_recebe_a_mensagem_da_familia()
    {
        var handler = new FakeHttpHandler()
            .Responde(XboxLive, HttpStatusCode.OK, RespostaDoXboxLive())
            .Responde(Xsts, HttpStatusCode.Unauthorized, new { XErr = 2148916238L });

        var result = await Build(handler).ExchangeAsync("token-da-microsoft", Ct);

        result.Message!.ShouldContain("família");
    }

    [Fact]
    public async Task Resposta_sem_token_falha_em_vez_de_seguir()
    {
        var handler = new FakeHttpHandler()
            .Responde(XboxLive, HttpStatusCode.OK, new { DisplayClaims = new { xui = new[] { new { uhs = "h" } } } });

        var result = await Build(handler).ExchangeAsync("token-da-microsoft", Ct);

        result.Outcome.ShouldBe(MinecraftTokenOutcome.Failed);

        // Não chegou ao XSTS: seguir com token nulo daria NullReference no salto
        // seguinte, e quem chamou veria um erro sem relação com a causa.
        handler.Pedidos.Count.ShouldBe(1);
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

    private static MinecraftTokenExchange Build(FakeHttpHandler handler) =>
        new(new HttpClient(handler), NullLogger<MinecraftTokenExchange>.Instance);
}
