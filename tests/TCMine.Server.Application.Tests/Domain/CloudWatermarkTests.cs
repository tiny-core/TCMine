using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Tests.Domain;

/// <summary>
///     A marca d'água é a régua do rollback: lote aplicado só a faz subir, e só
///     a decisão do dono a faz descer. Um sentido trocado ou abria incidente
///     falso a cada lote, ou deixava passar um mundo que voltou no tempo.
/// </summary>
public sealed class CloudWatermarkTests
{
    [Fact]
    public void Subir_compara_epoca_antes_da_sequencia_e_nunca_desce()
    {
        var marca = Marca(2, 5);

        marca.Raise(2, 4).ShouldBeFalse("lote mais velho que a marca não a move");
        marca.Raise(1, 99).ShouldBeFalse("época menor perde mesmo com sequência maior");
        (marca.Epoch, marca.Seq).ShouldBe((2L, 5L));

        marca.Raise(3, 1).ShouldBeTrue("época nova ganha mesmo com sequência menor");
        (marca.Epoch, marca.Seq).ShouldBe((3L, 1L));
    }

    [Fact]
    public void Descer_vai_ate_o_checkpoint_e_nunca_sobe()
    {
        var marca = Marca(2, 5);

        marca.LowerTo(3, 0).ShouldBeFalse("baixar para cima não é baixar");
        marca.LowerTo(2, 5).ShouldBeFalse();

        marca.LowerTo(1, 7).ShouldBeTrue();
        (marca.Epoch, marca.Seq).ShouldBe((1L, 7L));
    }

    private static CloudWatermark Marca(long epoch, long seq) =>
        CloudWatermark.At(Guid.CreateVersion7(), Guid.CreateVersion7(), "069a79f444e94726a5befca90e38aaf5", epoch,
            seq);
}
