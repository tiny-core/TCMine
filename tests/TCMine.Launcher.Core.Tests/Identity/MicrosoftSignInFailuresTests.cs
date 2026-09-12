using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Identity;

namespace TCMine.Launcher.Core.Tests.Identity;

/// <summary>
///     O que o jogador vê quando o login com a Microsoft não dá certo.
///     São decisões de produto, não detalhes do MSAL, e é por isso que a tradução
///     mora no Core e tem teste: o desfecho escolhido aqui decide se a tela se
///     cala, se oferece "tentar de novo", ou se manda falar com o administrador.
/// </summary>
public class MicrosoftSignInFailuresTests
{
    [Fact]
    public void Fechar_a_janela_do_navegador_nao_e_erro()
    {
        // O caso mais comum de todos, e o mais fácil de errar: tratá-lo como
        // falha faz a tela acusar o jogador de um erro que foi uma decisão dele.
        var resultado = MicrosoftSignInFailures.Traduzir(
            MicrosoftSignInFailures.Cancelled, "User canceled authentication.");

        resultado.Outcome.ShouldBe(AuthOutcome.Cancelled);

        // Sem mensagem de propósito: a tela não tem nada a dizer aqui.
        resultado.Message.ShouldBeNull();
    }

    [Fact]
    public void App_mal_registada_manda_falar_com_o_administrador()
    {
        // O jogador não tem como resolver isto. Sem dizê-lo, ele tenta de novo
        // indefinidamente contra uma app que nunca vai aceitar.
        var resultado = MicrosoftSignInFailures.Traduzir(
            MicrosoftSignInFailures.AppMalConfigurada, "AADSTS7000218");

        resultado.Outcome.ShouldBe(AuthOutcome.Failed);
        resultado.Message!.ShouldContain("administrador");
    }

    [Fact]
    public void Porta_ocupada_diz_o_que_fechar()
    {
        var resultado = MicrosoftSignInFailures.Traduzir(MicrosoftSignInFailures.LoopbackOcupado, null);

        resultado.Outcome.ShouldBe(AuthOutcome.Failed);
        resultado.Message!.ShouldContain("porta");
    }

    [Fact]
    public void Erro_desconhecido_carrega_o_detalhe_original()
    {
        // Nos casos previstos o detalhe atrapalha; neste ele é a única pista que
        // alguém vai ter para descobrir o que aconteceu.
        var resultado = MicrosoftSignInFailures.Traduzir("algo_novo", "servidor em manutenção");

        resultado.Outcome.ShouldBe(AuthOutcome.Failed);
        resultado.Message!.ShouldContain("servidor em manutenção");
    }

    [Fact]
    public void Erro_desconhecido_sem_detalhe_ainda_diz_alguma_coisa()
    {
        var resultado = MicrosoftSignInFailures.Traduzir(null, "   ");

        resultado.Outcome.ShouldBe(AuthOutcome.Failed);
        resultado.Message.ShouldNotBeNullOrWhiteSpace();
    }
}
