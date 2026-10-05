using Microsoft.EntityFrameworkCore;
using TCMine.Server.Application.Cloud;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Cloud;
using TCMine.Server.Web.Tests.Infrastructure;
using static TCMine.Server.Web.Tests.Infrastructure.CloudApi;

namespace TCMine.Server.Web.Tests.Endpoints;

/// <summary>
///     As decisões do dono no painel, de ponta a ponta: o que a API gravou
///     (quarentena, operação em dúvida, incidente) e o que cada decisão faz nos
///     saldos e no histórico. Todas só valem com o canal livre.
/// </summary>
public sealed class CloudGovernanceFlowTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Lote_de_epoca_velha_aplicado_pelo_dono_entra_no_saldo()
    {
        await using var env = await CloudAmbiente.CriarAsync();
        var a = env.Cliente(env.ChaveA);
        var canal = (await Acquire(a)).Channels.Single().Id;
        env.Relogio.Avancar(TimeSpan.FromMinutes(31));
        var b = env.Cliente(env.ChaveB);
        await Acquire(b);
        (await Lote(a, Credito(canal, seq: 1, delta: 10, depois: 10))).Result.ShouldBe("quarantined");
        var quarentena = await UnicaQuarentenaAsync(env);

        // B ainda segura os canais: aplicar agora desincronizaria o B.
        (await env.PainelAsync<ResolveCloudQuarantine, Result>(u =>
            u.HandleAsync(env.Nuvem, quarentena, apply: true, Ct))).Succeeded.ShouldBeFalse();

        await PostAsync<CloudReleaseReply>(b, "/api/cloud/v1/leases/release", new CloudReleaseRequest(Jogador, 2, 0));
        (await env.PainelAsync<ResolveCloudQuarantine, Result>(u =>
            u.HandleAsync(env.Nuvem, quarentena, apply: true, Ct))).Succeeded.ShouldBeTrue();

        (await env.SaldoAsync(Diamante)).ShouldBe(10);
        await using var db = await env.DbAsync();
        (await db.CloudLedger.SingleAsync(Ct)).Source.ShouldBe(CloudLedgerSource.QuarantineApply);
        (await db.CloudQuarantine.SingleAsync(Ct)).Resolution.ShouldBe(CloudQuarantineResolution.Applied);
        (await db.CloudAdminAudit.CountAsync(a => a.Action == "quarantine.apply", Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task Quarentena_de_saldo_negativo_nao_pode_ser_aplicada_so_descartada()
    {
        await using var env = await CloudAmbiente.CriarAsync();
        var a = env.Cliente(env.ChaveA);
        var canal = (await Acquire(a)).Channels.Single().Id;
        await Lote(a, Credito(canal, seq: 1, delta: 5, depois: 5));
        await Lote(a, Debito(canal, seq: 2, delta: -6, depois: -1));
        await PostAsync<CloudReleaseReply>(a, "/api/cloud/v1/leases/release", new CloudReleaseRequest(Jogador, 1, 1));
        var quarentena = await UnicaQuarentenaAsync(env);

        var aplicar = await env.PainelAsync<ResolveCloudQuarantine, Result>(u =>
            u.HandleAsync(env.Nuvem, quarentena, apply: true, Ct));
        aplicar.Succeeded.ShouldBeFalse();
        aplicar.Error!.ShouldContain("ficaria");

        (await env.PainelAsync<ResolveCloudQuarantine, Result>(u =>
            u.HandleAsync(env.Nuvem, quarentena, apply: false, Ct))).Succeeded.ShouldBeTrue();
        (await env.SaldoAsync(Diamante)).ShouldBe(5);
    }

    [Fact]
    public async Task Operacao_em_duvida_devolvida_volta_para_a_nuvem_uma_vez_so()
    {
        await using var env = await CloudAmbiente.CriarAsync();
        var a = env.Cliente(env.ChaveA);
        var canal = (await Acquire(a)).Channels.Single().Id;
        await Lote(a, Credito(canal, seq: 1, delta: 10, depois: 10));
        await PostAsync<CloudReleaseReply>(a, "/api/cloud/v1/leases/release", new CloudReleaseRequest(Jogador, 1, 1));
        await PostOkAsync(a, "/api/cloud/v1/reports/doubtful",
            new CloudDoubtfulRequest("boot-7", [new CloudDoubtfulDto(Jogador, canal, Diamante, "RecentDebit", 3)]));

        await using (var db = await env.DbAsync())
        {
            var duvida = await db.CloudDoubtfulOperations.SingleAsync(Ct);
            (await env.PainelAsync<ResolveCloudDoubtful, Result>(u =>
                u.HandleAsync(env.Nuvem, duvida.Id, refund: true, Ct))).Succeeded.ShouldBeTrue();
            (await env.PainelAsync<ResolveCloudDoubtful, Result>(u =>
                u.HandleAsync(env.Nuvem, duvida.Id, refund: true, Ct))).Succeeded.ShouldBeFalse();
        }

        (await env.SaldoAsync(Diamante)).ShouldBe(13);
    }

    [Fact]
    public async Task Estorno_de_rollback_desfaz_so_o_que_veio_depois_do_checkpoint()
    {
        await using var env = await CloudAmbiente.CriarAsync();
        var mundo = Guid.CreateVersion7();
        var a = env.Cliente(env.ChaveA);
        await PostAsync<CloudHelloReply>(a, "/api/cloud/v1/hello", HelloDoMundo(mundo, null));
        var canal = (await Acquire(a)).Channels.Single().Id;
        await Lote(a, Credito(canal, seq: 1, delta: 64, depois: 64));
        await Lote(a, Debito(canal, seq: 2, delta: -14, depois: 50));

        // O mundo voltou para logo depois do lote 1 (o servidor ainda segura o lease).
        var reiniciado = env.Cliente(await env.RotacionarChaveAAsync());
        await PostAsync<CloudHelloReply>(reiniciado, "/api/cloud/v1/hello",
            HelloDoMundo(mundo, new() { [Jogador] = new CloudSeqPosition(1, 1) }));
        var incidente = await IncidenteAsync(env);

        var previa = await env.PainelAsync<ResolveCloudIncident, Result<IReadOnlyList<CloudRevertLine>>>(u =>
            u.PreviewAsync(env.Nuvem, incidente, Ct));
        var linha = previa.Value!.Single();
        linha.Delta.ShouldBe(14);
        linha.After.ShouldBe(64);

        (await env.PainelAsync<ResolveCloudIncident, Result>(u =>
            u.HandleAsync(env.Nuvem, incidente, revert: true, Ct))).Succeeded.ShouldBeTrue();

        (await env.SaldoAsync(Diamante)).ShouldBe(64);
        await using var db = await env.DbAsync();
        (await db.CloudBatches.SingleAsync(b => b.Seq == 2, Ct)).Status.ShouldBe(CloudBatchStatus.Reverted);
        (await db.CloudLeases.SingleAsync(Ct)).HolderServerId.ShouldBeNull("o servidor do incidente pega saldos novos no próximo acquire");
        // O mundo restaurado continua declarando o ponto em que está (lote 1): sem incidente novo.
        (await PostAsync<CloudHelloReply>(reiniciado, "/api/cloud/v1/hello",
                HelloDoMundo(mundo, new() { [Jogador] = new CloudSeqPosition(1, 1) }))).ReadOnly
            .ShouldBeFalse("incidente resolvido destrava a nuvem");
    }

    [Fact]
    public async Task Estorno_para_em_zero_quando_os_itens_ja_sairam_por_outro_servidor()
    {
        await using var env = await CloudAmbiente.CriarAsync();
        var mundo = Guid.CreateVersion7();
        var a = env.Cliente(env.ChaveA);
        await PostAsync<CloudHelloReply>(a, "/api/cloud/v1/hello", HelloDoMundo(mundo, null));
        var canal = (await Acquire(a)).Channels.Single().Id;
        await Lote(a, Credito(canal, seq: 1, delta: 64, depois: 64));
        await PostAsync<CloudReleaseReply>(a, "/api/cloud/v1/leases/release", new CloudReleaseRequest(Jogador, 1, 1));

        // O jogador foi para o B e tirou 60.
        var b = env.Cliente(env.ChaveB);
        await Acquire(b);
        await Lote(b, Debito(canal, seq: 1, delta: -60, depois: 4) with { Epoch = 2 });
        await PostAsync<CloudReleaseReply>(b, "/api/cloud/v1/leases/release", new CloudReleaseRequest(Jogador, 2, 1));

        // O mundo do A volta para antes de tudo.
        var reiniciado = env.Cliente(await env.RotacionarChaveAAsync());
        await PostAsync<CloudHelloReply>(reiniciado, "/api/cloud/v1/hello", HelloDoMundo(mundo, []));
        var incidente = await IncidenteAsync(env);

        var linha = (await env.PainelAsync<ResolveCloudIncident, Result<IReadOnlyList<CloudRevertLine>>>(u =>
            u.PreviewAsync(env.Nuvem, incidente, Ct))).Value!.Single();
        linha.Delta.ShouldBe(-4);
        linha.After.ShouldBe(0);
        linha.Shortfall.ShouldBe(60);

        (await env.PainelAsync<ResolveCloudIncident, Result>(u =>
            u.HandleAsync(env.Nuvem, incidente, revert: true, Ct))).Succeeded.ShouldBeTrue();
        (await env.SaldoAsync(Diamante)).ShouldBe(0);
    }

    [Fact]
    public async Task Regra_criada_no_painel_chega_ao_mod_com_versao_nova()
    {
        await using var env = await CloudAmbiente.CriarAsync();
        var a = env.Cliente(env.ChaveA);
        var antes = await PostAsync<CloudHelloReply>(a, "/api/cloud/v1/hello", Hello());

        (await env.PainelAsync<AddCloudRule, Result>(u => u.HandleAsync(env.Nuvem, CloudRuleScope.Tag,
            "#C:Shulker_Boxes", CloudRuleAction.Block, null, Ct))).Succeeded.ShouldBeTrue();

        var policy = await PostAsync<CloudPolicyReply>(a, "/api/cloud/v1/policy", new { });
        policy.PolicyVersion.ShouldBeGreaterThan(antes.PolicyVersion);
        policy.Rules.Single().ShouldBe(new CloudRuleDto("Tag", "c:shulker_boxes", "Block"));
    }

    private static async Task<Guid> UnicaQuarentenaAsync(CloudAmbiente env)
    {
        await using var db = await env.DbAsync();
        return (await db.CloudQuarantine.SingleAsync(Ct)).Id;
    }

    private static async Task<Guid> IncidenteAsync(CloudAmbiente env)
    {
        await using var db = await env.DbAsync();
        return (await db.CloudRollbackIncidents.SingleAsync(Ct)).Id;
    }
}
