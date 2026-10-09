using TCMine.Server.Domain.Cloud;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Tests.Domain;

public sealed class CloudEntitiesTests
{
    [Fact]
    public void Saldo_nunca_fica_negativo()
    {
        var saldo = new CloudBalance { ChannelId = Guid.CreateVersion7(), ItemTypeId = Guid.CreateVersion7() };
        saldo.Apply(10).ShouldBe(10);

        Should.Throw<InvalidOperationException>(() => saldo.Apply(-11));
        saldo.Amount.ShouldBe(10, "a falha não pode deixar o saldo pela metade");
    }

    [Fact]
    public void Servidor_so_liga_a_nuvem_do_mesmo_dono()
    {
        var dono = Guid.CreateVersion7();
        var servidor = NovoServidor(dono);
        var outraNuvem = new CloudVault { Name = "Alheia", OwnerId = Guid.CreateVersion7() };

        Should.Throw<InvalidOperationException>(() => servidor.AttachToCloudVault(outraNuvem));
        servidor.CloudVaultId.ShouldBeNull();

        var minhaNuvem = new CloudVault { Name = "Minha", OwnerId = dono };
        servidor.AttachToCloudVault(minhaNuvem);
        servidor.CloudVaultId.ShouldBe(minhaNuvem.Id);
    }

    [Fact]
    public void Mudar_o_modo_da_politica_aumenta_a_versao()
    {
        var nuvem = new CloudVault { Name = "N", OwnerId = Guid.CreateVersion7() };
        var antes = nuvem.PolicyVersion;

        nuvem.SetPolicyMode(CloudPolicyMode.Allowlist);
        nuvem.SetPolicyMode(CloudPolicyMode.Allowlist);

        nuvem.PolicyVersion.ShouldBe(antes + 1, "repetir o mesmo modo não é mudança");
    }

    [Fact]
    public void Limites_fora_de_faixa_sao_recusados_sem_mudar_nada()
    {
        var nuvem = new CloudVault { Name = "N", OwnerId = Guid.CreateVersion7() };

        Should.Throw<ArgumentOutOfRangeException>(() => nuvem.UpdateLimits(0, 8192, 5, 100, 1000));
        nuvem.LeaseTtlMinutes.ShouldBe(30);
    }

    [Theory]
    [InlineData("069A79F4-44E9-4726-A5BE-FCA90E38AAF5", "069a79f444e94726a5befca90e38aaf5")]
    [InlineData("069a79f444e94726a5befca90e38aaf5", "069a79f444e94726a5befca90e38aaf5")]
    [InlineData("nao-e-uuid", null)]
    [InlineData("", null)]
    public void Uuid_do_jogador_e_normalizado(string bruto, string? esperado) =>
        CloudChannel.NormalizePlayerUuid(bruto).ShouldBe(esperado);

    [Fact]
    public void Quarentena_so_se_resolve_uma_vez()
    {
        var q = new CloudQuarantine
        {
            VaultId = Guid.CreateVersion7(),
            BatchId = Guid.CreateVersion7(),
            Reason = CloudQuarantineReason.StaleEpoch,
            Detail = "x",
            PayloadJson = "{}"
        };
        q.Resolve(CloudQuarantineResolution.Discarded, Guid.CreateVersion7(), DateTimeOffset.UtcNow);

        Should.Throw<InvalidOperationException>(() =>
            q.Resolve(CloudQuarantineResolution.Applied, Guid.CreateVersion7(), DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData(CloudRuleScope.Item, "Minecraft:Diamond", "minecraft:diamond")]
    [InlineData(CloudRuleScope.Tag, "#c:shulker_boxes", "c:shulker_boxes")]
    [InlineData(CloudRuleScope.Mod, " RefinedStorage ", "refinedstorage")]
    [InlineData(CloudRuleScope.Mod, "minecraft:dirt", null)]
    [InlineData(CloudRuleScope.Item, "semnamespace", null)]
    [InlineData(CloudRuleScope.Item, "mod:", null)]
    [InlineData(CloudRuleScope.Tag, "com espaco:x", null)]
    public void Padrao_da_regra_e_normalizado_por_escopo(CloudRuleScope scope, string bruto, string? esperado) =>
        CloudItemRule.NormalizePattern(scope, bruto).ShouldBe(esperado);

    private static GameServer NovoServidor(Guid dono) => new()
    {
        Name = "S",
        ModpackId = Guid.CreateVersion7(),
        ModpackVersionId = Guid.CreateVersion7(),
        ConnectAddress = "localhost",
        RconSecret = "segredo",
        OwnerId = dono
    };
}
