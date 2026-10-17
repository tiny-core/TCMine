using TCMine.Server.Application.Cloud;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Tests.Cloud;

/// <summary>
///     Auditoria da nuvem (5.2): o caso de uso valida e normaliza os filtros
///     antes do banco, e nunca deixa um dono ver o histórico de outro.
/// </summary>
public sealed class CloudAuditUseCasesTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Uuid_com_hifens_e_maiusculas_chega_normalizado_ao_banco()
    {
        var (useCase, governance, nuvem) = Montar();

        var result = await useCase.HandleAsync(nuvem,
            new CloudLedgerFilter(" 069A79F4-44E9-4726-A5BE-FCA90E38AAF5 ", "  Diamond  "), null, Ct);

        result.Succeeded.ShouldBeTrue();
        governance.LastLedgerQuery!.PlayerUuid.ShouldBe("069a79f444e94726a5befca90e38aaf5");
        governance.LastLedgerQuery.Item.ShouldBe("Diamond");
        governance.LastLedgerQuery.PageSize.ShouldBe(ListCloudLedger.PageSize);
    }

    [Fact]
    public async Task Uuid_invalido_e_recusado_em_vez_de_virar_sem_filtro()
    {
        // O bug antigo: um UUID digitado errado era ignorado e a tela mostrava a
        // nuvem inteira, como se fosse o histórico daquele jogador.
        var (useCase, governance, nuvem) = Montar();

        var result = await useCase.HandleAsync(nuvem, new CloudLedgerFilter("ana"), null, Ct);

        result.Succeeded.ShouldBeFalse();
        result.Error.ShouldBe(ListCloudLedger.InvalidPlayer);
        governance.LastLedgerQuery.ShouldBeNull("nada pode chegar ao banco com o filtro recusado");
    }

    [Fact]
    public async Task Periodo_invertido_e_recusado()
    {
        var (useCase, governance, nuvem) = Montar();
        var agora = DateTimeOffset.UtcNow;

        var result = await useCase.HandleAsync(nuvem,
            new CloudLedgerFilter(From: agora, Until: agora.AddDays(-1)), null, Ct);

        result.Succeeded.ShouldBeFalse();
        governance.LastLedgerQuery.ShouldBeNull();
    }

    [Fact]
    public async Task Historico_de_nuvem_alheia_responde_como_inexistente()
    {
        var repo = new FakeCloudAdminRepository();
        var governance = new FakeCloudGovernance();
        var alheia = new CloudVault { Name = "Alheia", OwnerId = Guid.CreateVersion7() };
        repo.Vaults.Add(alheia);
        var eu = new FakeUserScope();

        var ledger = await new ListCloudLedger(repo, governance, eu)
            .HandleAsync(alheia.Id, new CloudLedgerFilter(), null, Ct);
        var acoes = await new ListCloudAdminActions(repo, governance, eu).HandleAsync(alheia.Id, null, Ct);

        ledger.Error.ShouldBe(CloudVaultAccess.NotFound);
        acoes.Error.ShouldBe(CloudVaultAccess.NotFound);
        governance.LastLedgerQuery.ShouldBeNull();
    }

    [Fact]
    public void Pagina_com_uma_linha_a_mais_corta_e_aponta_o_cursor_para_a_ultima_mostrada()
    {
        Guid[] ids = [Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7()];

        var cheia = CursorPage.From(ids, 2, id => id);
        cheia.Items.ShouldBe([ids[0], ids[1]]);
        cheia.Next.ShouldBe(ids[1], "a próxima página começa logo depois da última linha mostrada");

        var ultima = CursorPage.From(ids, 3, id => id);
        ultima.Items.Count.ShouldBe(3);
        ultima.Next.ShouldBeNull("sem a linha de sobra, não há página seguinte");
    }

    private static (ListCloudLedger UseCase, FakeCloudGovernance Governance, Guid Vault) Montar()
    {
        var repo = new FakeCloudAdminRepository();
        var governance = new FakeCloudGovernance();
        var eu = new FakeUserScope();
        var nuvem = new CloudVault { Name = "Minha", OwnerId = eu.OwnerId };
        repo.Vaults.Add(nuvem);
        return (new ListCloudLedger(repo, governance, eu), governance, nuvem.Id);
    }
}
