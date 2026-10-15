using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Dns;

namespace TCMine.Server.Application.Tests.Dns;

/// <summary>
///     A lista de entradas do túnel é trocada INTEIRA a cada gravação. Cada caso
///     abaixo é uma forma de estragar o túnel do admin — e a garantia de que
///     não acontece.
/// </summary>
public sealed class TunnelIngressPlanTests
{
    private static readonly TunnelIngressRule Fim = new(null, TunnelIngressPlan.CatchAllService, null, "{}");

    [Fact]
    public void Entrada_nova_entra_antes_da_regra_final()
    {
        var r = TunnelIngressPlan.Merge([Fim], Pedidas(("a.exemplo.com", "tcp://localhost:25570")), Nossas());

        Assert.True(r.Changed);
        Assert.Equal("a.exemplo.com", r.Rules[0].Hostname);
        Assert.Equal(TunnelIngressPlan.CatchAllService, r.Rules[^1].Service);
    }

    [Fact]
    public void Entrada_do_admin_fica_e_volta_com_o_json_original()
    {
        var painel = new TunnelIngressRule("tcmine.exemplo.com", "http://localhost:8080", null, "{\"x\":1}");

        var r = TunnelIngressPlan.Merge([painel, Fim], Pedidas(("a.exemplo.com", "tcp://localhost:25570")), Nossas());

        Assert.Same(painel, r.Rules[0]);
    }

    [Fact]
    public void Nome_do_admin_com_outro_destino_e_conflito_e_nao_e_trocado()
    {
        var doAdmin = new TunnelIngressRule("a.exemplo.com", "tcp://192.168.1.50:25565", null, "{}");

        var r = TunnelIngressPlan.Merge([doAdmin, Fim], Pedidas(("a.exemplo.com", "tcp://localhost:25570")), Nossas());

        Assert.Same(doAdmin, r.Rules[0]);
        Assert.Single(r.Problems);
        Assert.False(r.Changed);
    }

    [Fact]
    public void Nome_do_admin_com_o_mesmo_destino_e_adotado_sem_conflito()
    {
        // É o caso da entrada que o admin já tinha criado à mão para o mesmo
        // servidor: nada a mudar, e ela passa a ser mantida pelo TCMine.
        var doAdmin = new TunnelIngressRule("a.exemplo.com", "tcp://localhost:25570", null, "{}");

        var r = TunnelIngressPlan.Merge([doAdmin, Fim], Pedidas(("a.exemplo.com", "tcp://localhost:25570")), Nossas());

        Assert.Empty(r.Problems);
        Assert.False(r.Changed);
    }

    [Fact]
    public void Entrada_nossa_que_mudou_de_porta_e_trocada()
    {
        var antiga = new TunnelIngressRule("a.exemplo.com", "tcp://localhost:25570", null, "{}");

        var r = TunnelIngressPlan.Merge(
            [antiga, Fim], Pedidas(("a.exemplo.com", "tcp://localhost:25571")), Nossas("a.exemplo.com"));

        Assert.True(r.Changed);
        Assert.Equal("tcp://localhost:25571", r.Rules[0].Service);
    }

    [Fact]
    public void Entrada_nossa_que_ninguem_mais_pede_sai()
    {
        var antiga = new TunnelIngressRule("a.exemplo.com", "tcp://localhost:25570", null, "{}");

        var r = TunnelIngressPlan.Merge([antiga, Fim], Pedidas(), Nossas("a.exemplo.com"));

        Assert.True(r.Changed);
        Assert.Equal([Fim], r.Rules);
    }

    [Fact]
    public void Lista_sem_regra_final_ganha_uma()
    {
        // A Cloudflare recusa a gravação sem ela.
        var r = TunnelIngressPlan.Merge([], Pedidas(), Nossas());

        Assert.True(r.Changed);
        Assert.Equal(TunnelIngressPlan.CatchAllService, Assert.Single(r.Rules).Service);
    }

    [Fact]
    public void Tudo_igual_nao_grava()
    {
        var nossa = new TunnelIngressRule("a.exemplo.com", "tcp://localhost:25570", null, "{}");

        var r = TunnelIngressPlan.Merge(
            [nossa, Fim], Pedidas(("a.exemplo.com", "tcp://localhost:25570")), Nossas("a.exemplo.com"));

        Assert.False(r.Changed);
    }

    private static Dictionary<string, string> Pedidas(params (string Host, string Service)[] items) =>
        items.ToDictionary(i => i.Host, i => i.Service, StringComparer.OrdinalIgnoreCase);

    private static HashSet<string> Nossas(params string[] hosts) => new(hosts, StringComparer.OrdinalIgnoreCase);
}
