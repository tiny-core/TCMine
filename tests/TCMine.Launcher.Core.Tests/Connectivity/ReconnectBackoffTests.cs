using TCMine.Launcher.Core.Connectivity;

namespace TCMine.Launcher.Core.Tests.Connectivity;

public sealed class ReconnectBackoffTests
{
    [Fact]
    public void A_primeira_tentativa_e_imediata()
    {
        // A queda mais comum é um soluço de rede que já passou.
        ReconnectBackoff.DelayFor(0).ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void A_espera_cresce_ate_o_teto_e_fica_nele()
    {
        ReconnectBackoff.DelayFor(1).ShouldBe(TimeSpan.FromSeconds(2));
        ReconnectBackoff.DelayFor(4).ShouldBe(TimeSpan.FromSeconds(30));
        ReconnectBackoff.DelayFor(5).ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Nunca_desiste_nem_passa_do_teto()
    {
        // Regressão: a lista antiga acabava na quinta tentativa, e o launcher
        // ficava desligado para sempre depois de um minuto de servidor fora.
        ReconnectBackoff.DelayFor(100_000).ShouldBe(ReconnectBackoff.Ceiling);
        ReconnectBackoff.DelayFor(long.MaxValue).ShouldBe(ReconnectBackoff.Ceiling);
    }

    [Fact]
    public void Contagem_negativa_nao_quebra()
    {
        ReconnectBackoff.DelayFor(-1).ShouldBe(TimeSpan.Zero);
    }
}
