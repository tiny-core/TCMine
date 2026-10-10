using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Dns;

namespace TCMine.Server.Application.Tests.Dns;

/// <summary>
///     A diferença entre o que a zona tem e o que deveria ter. É aqui que se
///     decide o que é apagado na zona do admin, então cada caso de "não apague"
///     tem o seu teste.
/// </summary>
public sealed class DnsPlanTests
{
    private const string Host = "tcmine:abc:host";
    private const string Srv1 = "tcmine:abc:srv:1";
    private const string Srv2 = "tcmine:abc:srv:2";

    [Fact]
    public void Zona_vazia_cria_tudo()
    {
        var plan = DnsPlan.Build([A("1.2.3.4"), Srv(Srv1, "a", 25565)], [], null);

        Assert.Equal(2, plan.Create.Count);
        Assert.Empty(plan.Replace);
        Assert.Empty(plan.Delete);
    }

    [Fact]
    public void Zona_ja_certa_nao_gera_nenhuma_chamada()
    {
        // Roda de tempos em tempos: se "igual" gerasse escrita, cada rodada
        // reescreveria a zona inteira.
        var plan = DnsPlan.Build(
            [A("1.2.3.4"), Srv(Srv1, "a", 25565)],
            [new DnsRecord("r1", A("1.2.3.4")), new DnsRecord("r2", Srv(Srv1, "a", 25565))],
            null);

        Assert.Empty(plan.Create);
        Assert.Empty(plan.Replace);
        Assert.Empty(plan.Delete);
        Assert.Equal(2, plan.Unchanged);
    }

    [Fact]
    public void Maiusculas_no_nome_nao_contam_como_diferenca()
    {
        var existing = new DnsRecord("r2", Srv(Srv1, "a", 25565) with { Name = "_MINECRAFT._tcp.A.exemplo.com" });

        var plan = DnsPlan.Build([Srv(Srv1, "a", 25565)], [existing], null);

        Assert.Empty(plan.Replace);
    }

    [Fact]
    public void Ip_novo_atualiza_o_registro_a_no_lugar()
    {
        var plan = DnsPlan.Build([A("5.6.7.8")], [new DnsRecord("r1", A("1.2.3.4"))], null);

        var change = Assert.Single(plan.Replace);
        Assert.Equal("r1", change.RecordId);
        Assert.Equal("5.6.7.8", change.Record.Target);
        Assert.Empty(plan.Create);
        Assert.Empty(plan.Delete);
    }

    [Fact]
    public void Trocar_a_porta_ou_o_subdominio_e_alteracao_e_nao_apaga_e_cria()
    {
        // A identidade é o comentário (o servidor), não o nome: o registro muda
        // de nome sem deixar de existir por um instante.
        var plan = DnsPlan.Build(
            [Srv(Srv1, "novo", 25570)],
            [new DnsRecord("r2", Srv(Srv1, "antigo", 25565))],
            null);

        var change = Assert.Single(plan.Replace);
        Assert.Equal("r2", change.RecordId);
        Assert.Equal(25570, change.Record.Port);
        Assert.Empty(plan.Create);
        Assert.Empty(plan.Delete);
    }

    [Fact]
    public void Servidor_apagado_ou_sem_subdominio_perde_o_registro()
    {
        var plan = DnsPlan.Build(
            [Srv(Srv1, "a", 25565)],
            [new DnsRecord("r2", Srv(Srv1, "a", 25565)), new DnsRecord("r3", Srv(Srv2, "b", 25566))],
            null);

        Assert.Equal("r3", Assert.Single(plan.Delete).Id);
    }

    [Fact]
    public void Registro_a_e_poupado_quando_o_ip_nao_foi_detectado()
    {
        // Sem IP o registro A sai da lista de desejados — mas não porque
        // deixou de ser preciso. Apagá-lo derrubaria todos os servidores por
        // causa de uma consulta que falhou.
        var plan = DnsPlan.Build(
            [Srv(Srv1, "a", 25565)],
            [new DnsRecord("r1", A("1.2.3.4")), new DnsRecord("r2", Srv(Srv1, "a", 25565))],
            Host);

        Assert.Empty(plan.Delete);
    }

    [Fact]
    public void Registro_duplicado_com_a_mesma_marca_e_sobra()
    {
        var plan = DnsPlan.Build(
            [Srv(Srv1, "a", 25565)],
            [new DnsRecord("r2", Srv(Srv1, "a", 25565)), new DnsRecord("r9", Srv(Srv1, "a", 25565))],
            null);

        Assert.Equal("r9", Assert.Single(plan.Delete).Id);
        Assert.Equal(1, plan.Unchanged);
    }

    private static DnsRecordSpec A(string ip) => new(DnsRecordTypes.A, "mc.exemplo.com", ip, 0, Host);

    private static DnsRecordSpec Srv(string comment, string sub, int port) =>
        new(DnsRecordTypes.Srv, $"_minecraft._tcp.{sub}.exemplo.com", "mc.exemplo.com", port, comment);
}
