using TCMine.Server.Application.Dns;
using TCMine.Server.Web.Background;

namespace TCMine.Server.Web.Tests.Background;

/// <summary>
///     O sinal entre quem pede a sincronização de DNS e o serviço que a executa.
///     Dois erros possíveis, e os dois calados: perder um pedido deixa um
///     servidor novo sem nome até a rodada periódica; não juntar os pedidos
///     transforma dez edições seguidas em dez idas à Cloudflare.
/// </summary>
public sealed class DnsSyncStateTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Pedido_acorda_a_espera_antes_do_intervalo()
    {
        var state = new DnsSyncState();
        state.Request();

        (await state.WaitAsync(TimeSpan.FromMinutes(5), Ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task Sem_pedido_a_espera_termina_no_intervalo()
    {
        var state = new DnsSyncState();

        (await state.WaitAsync(TimeSpan.FromMilliseconds(30), Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Varios_pedidos_seguidos_viram_um_so()
    {
        var state = new DnsSyncState();
        state.Request();
        state.Request();
        state.Request();

        (await state.WaitAsync(TimeSpan.FromMinutes(5), Ct)).ShouldBeTrue();
        (await state.WaitAsync(TimeSpan.FromMilliseconds(30), Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Drenar_esquece_os_pedidos_ja_atendidos()
    {
        var state = new DnsSyncState();
        state.Request();

        state.Drain();

        (await state.WaitAsync(TimeSpan.FromMilliseconds(30), Ct)).ShouldBeFalse();
    }

    [Fact]
    public void Guarda_o_ultimo_resultado_para_a_tela()
    {
        var state = new DnsSyncState();
        state.Last.ShouldBeNull();

        state.Record(DnsSyncReport.Disabled);

        state.Last.ShouldNotBeNull();
        state.Last!.Report.ShouldBe(DnsSyncReport.Disabled);
    }
}
