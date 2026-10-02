using TCMine.Server.Application.Cloud;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Tests.Cloud;

/// <summary>
///     Cada regra que recusa um lote. Um lote recusado vai INTEIRO para a
///     quarentena: aplicar metade deixaria o servidor de jogo e o banco
///     discordando sem ninguém saber.
/// </summary>
public sealed class CloudBatchDecisionTests
{
    private static readonly string Diamante = new('d', 64);
    private static readonly string Ferro = new('f', 64);
    private static readonly CloudChannel Canal = NovoCanal();
    private static readonly Guid DiamanteId = Guid.CreateVersion7();

    private static CloudChannel NovoCanal() =>
        new() { VaultId = Guid.CreateVersion7(), PlayerUuid = new string('a', 32), Name = CloudChannel.DefaultName };

    private static CloudBatchDecision.State Estado(long saldoDiamante = 10, CloudChannel? canal = null,
        int maxTipos = 100, long maxTotal = 1_000_000)
    {
        var c = canal ?? Canal;
        return new CloudBatchDecision.State(
            new Dictionary<Guid, CloudChannel> { [c.Id] = c },
            new Dictionary<string, Guid>(StringComparer.Ordinal) { [Diamante] = DiamanteId },
            new Dictionary<(Guid, string), long> { [(c.Id, Diamante)] = saldoDiamante },
            maxTipos, maxTotal, 8192);
    }

    private static CloudBatchRequest Lote(IEnumerable<CloudOpDto> ops, IEnumerable<CloudExpectedDto> esperado,
        IEnumerable<CloudItemDto>? definicoes = null) =>
        new(new string('a', 32), 1, 1, ops.ToList(), esperado.ToList(), definicoes?.ToList());

    private static CloudItemDto DefinicaoFerro() => new(Ferro, "minecraft:iron_ingot", "Ferro", Convert.ToBase64String([1, 2, 3]));

    [Fact]
    public void Credito_e_debito_validos_sao_aceitos_com_o_saldo_final()
    {
        var lote = Lote(
            [new(Canal.Id, Diamante, -4), new(Canal.Id, Ferro, 64)],
            [new(Canal.Id, Diamante, 6), new(Canal.Id, Ferro, 64)],
            [DefinicaoFerro()]);

        var aceito = CloudBatchDecision.Decide(lote, Estado()).ShouldBeOfType<CloudBatchDecision.Accepted>();

        aceito.Changes.Select(c => c.After).ShouldBe([6L, 64L]);
        aceito.NewItems.Single().Fingerprint.ShouldBe(Ferro);
    }

    [Fact]
    public void Debito_alem_do_saldo_vai_para_quarentena()
    {
        var lote = Lote([new(Canal.Id, Diamante, -11)], [new(Canal.Id, Diamante, -1)]);

        CloudBatchDecision.Decide(lote, Estado()).ShouldBeOfType<CloudBatchDecision.Rejected>()
            .Reason.ShouldBe(CloudQuarantineReason.NegativeBalance);
    }

    [Fact]
    public void Saldo_esperado_diferente_e_divergencia()
    {
        var lote = Lote([new(Canal.Id, Diamante, -1)], [new(Canal.Id, Diamante, 8)]);

        CloudBatchDecision.Decide(lote, Estado()).ShouldBeOfType<CloudBatchDecision.Rejected>()
            .Reason.ShouldBe(CloudQuarantineReason.Divergence);
    }

    [Fact]
    public void Saldo_esperado_ausente_tambem_e_divergencia()
    {
        var lote = Lote([new(Canal.Id, Diamante, -1)], []);

        CloudBatchDecision.Decide(lote, Estado()).ShouldBeOfType<CloudBatchDecision.Rejected>()
            .Reason.ShouldBe(CloudQuarantineReason.Divergence);
    }

    [Fact]
    public void Canal_de_outro_jogador_e_recusado()
    {
        var alheio = Guid.CreateVersion7();
        var lote = Lote([new(alheio, Diamante, 1)], [new(alheio, Diamante, 1)]);

        CloudBatchDecision.Decide(lote, Estado()).ShouldBeOfType<CloudBatchDecision.Rejected>()
            .Reason.ShouldBe(CloudQuarantineReason.UnknownChannel);
    }

    [Fact]
    public void Canal_congelado_nao_aceita_nada()
    {
        var canal = NovoCanal();
        canal.Freeze("teste");
        var lote = Lote([new(canal.Id, Diamante, 1)], [new(canal.Id, Diamante, 11)]);

        CloudBatchDecision.Decide(lote, Estado(canal: canal)).ShouldBeOfType<CloudBatchDecision.Rejected>()
            .Reason.ShouldBe(CloudQuarantineReason.FrozenChannel);
    }

    [Fact]
    public void Item_desconhecido_sem_definicao_e_recusado()
    {
        var lote = Lote([new(Canal.Id, Ferro, 5)], [new(Canal.Id, Ferro, 5)]);

        CloudBatchDecision.Decide(lote, Estado()).ShouldBeOfType<CloudBatchDecision.Rejected>()
            .Reason.ShouldBe(CloudQuarantineReason.UnknownItem);
    }

    [Fact]
    public void Item_desconhecido_nao_pode_sair()
    {
        // Saída de algo que o banco nunca viu entrar: só pode ser invenção.
        var lote = Lote([new(Canal.Id, Ferro, -5)], [new(Canal.Id, Ferro, 0)], [DefinicaoFerro()]);

        CloudBatchDecision.Decide(lote, Estado()).ShouldBeOfType<CloudBatchDecision.Rejected>()
            .Reason.ShouldBe(CloudQuarantineReason.UnknownItem);
    }

    [Theory]
    [InlineData("curta", "minecraft:x", "AQID")]
    [InlineData(null, "sem_namespace", "AQID")]
    [InlineData(null, "minecraft:x", "nao e base64!")]
    public void Definicao_invalida_e_recusada(string? fingerprint, string itemId, string encoded)
    {
        var fp = fingerprint ?? Ferro;
        var lote = Lote([new(Canal.Id, fp, 1)], [new(Canal.Id, fp, 1)], [new(fp, itemId, "X", encoded)]);

        CloudBatchDecision.Decide(lote, Estado()).ShouldBeOfType<CloudBatchDecision.Rejected>()
            .Reason.ShouldBe(CloudQuarantineReason.UnknownItem);
    }

    [Fact]
    public void Item_maior_que_o_limite_da_nuvem_e_recusado()
    {
        var grande = new CloudItemDto(Ferro, "minecraft:x", "X", Convert.ToBase64String(new byte[9000]));
        var lote = Lote([new(Canal.Id, Ferro, 1)], [new(Canal.Id, Ferro, 1)], [grande]);

        CloudBatchDecision.Decide(lote, Estado()).ShouldBeOfType<CloudBatchDecision.Rejected>()
            .Reason.ShouldBe(CloudQuarantineReason.UnknownItem);
    }

    [Fact]
    public void Cota_de_tipos_e_de_total()
    {
        var umTipoNovo = Lote([new(Canal.Id, Ferro, 1)], [new(Canal.Id, Ferro, 1)], [DefinicaoFerro()]);
        CloudBatchDecision.Decide(umTipoNovo, Estado(maxTipos: 1)).ShouldBeOfType<CloudBatchDecision.Rejected>()
            .Reason.ShouldBe(CloudQuarantineReason.QuotaExceeded);

        var muitos = Lote([new(Canal.Id, Diamante, 91)], [new(Canal.Id, Diamante, 101)]);
        CloudBatchDecision.Decide(muitos, Estado(maxTotal: 100)).ShouldBeOfType<CloudBatchDecision.Rejected>()
            .Reason.ShouldBe(CloudQuarantineReason.QuotaExceeded);
    }

    [Fact]
    public void Debito_nao_e_barrado_pela_cota()
    {
        // A cota baixou depois que o jogador já tinha os itens: tirar tem de
        // continuar possível, senão o jogador fica preso acima do limite.
        var lote = Lote([new(Canal.Id, Diamante, -1)], [new(Canal.Id, Diamante, 9)]);

        CloudBatchDecision.Decide(lote, Estado(maxTotal: 5)).ShouldBeOfType<CloudBatchDecision.Accepted>();
    }

    [Fact]
    public void Operacao_zerada_ou_item_repetido_e_recusado()
    {
        var zerada = Lote([new(Canal.Id, Diamante, 0)], [new(Canal.Id, Diamante, 10)]);
        CloudBatchDecision.Decide(zerada, Estado()).ShouldBeOfType<CloudBatchDecision.Rejected>();

        var repetido = Lote([new(Canal.Id, Diamante, 1), new(Canal.Id, Diamante, 1)], [new(Canal.Id, Diamante, 12)]);
        CloudBatchDecision.Decide(repetido, Estado()).ShouldBeOfType<CloudBatchDecision.Rejected>()
            .Reason.ShouldBe(CloudQuarantineReason.PayloadMismatch);
    }
}
