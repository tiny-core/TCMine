using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Cloud;

/// <summary>
///     Até onde o mundo de UM servidor está confirmado para UM jogador, em
///     (época, seq) — a régua da detecção de rollback no hello: um checkpoint
///     ATRÁS da marca é um mundo que voltou no tempo.
///     Sobe quando um lote deste servidor é aplicado (pelo jogo ou pelo dono, na
///     quarentena) e só desce por decisão do dono num incidente: estornar ou
///     aceitar o mundo restaurado põe a marca no ponto do checkpoint dele.
///     Existe para o hello não ler o histórico inteiro de lotes (que crescia sem
///     parar e não podia expirar) e para "aceitar" valer: medida pelos lotes, a
///     régua não mudava ao aceitar, e o mesmo incidente voltava no hello seguinte.
/// </summary>
public sealed class CloudWatermark : Entity
{
    public required Guid VaultId { get; init; }

    public required Guid ServerId { get; init; }

    public required string PlayerUuid { get; init; }

    public long Epoch { get; private set; }

    public long Seq { get; private set; }

    public static CloudWatermark At(Guid vaultId, Guid serverId, string playerUuid, long epoch, long seq) =>
        new() { VaultId = vaultId, ServerId = serverId, PlayerUuid = playerUuid, Epoch = epoch, Seq = seq };

    /// <summary>Sobe até (época, seq); nunca desce. Devolve se mudou.</summary>
    public bool Raise(long epoch, long seq)
    {
        if ((epoch, seq).CompareTo((Epoch, Seq)) <= 0)
            return false;
        (Epoch, Seq) = (epoch, seq);
        Touch();
        return true;
    }

    /// <summary>Desce até (época, seq); nunca sobe. Devolve se mudou.</summary>
    public bool LowerTo(long epoch, long seq)
    {
        if ((epoch, seq).CompareTo((Epoch, Seq)) >= 0)
            return false;
        (Epoch, Seq) = (epoch, seq);
        Touch();
        return true;
    }
}
