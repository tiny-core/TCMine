using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Cloud;

/// <summary>
///     Registro de um lote recebido de um servidor de jogo. Índice único em
///     (VaultId, PlayerUuid, Epoch, Seq): o mesmo lote nunca entra duas vezes,
///     mesmo que duas requisições cheguem juntas. O hash do conteúdo distingue
///     um reenvio legítimo (mesmo hash) de um lote diferente com o mesmo número
///     (bug ou trapaça).
/// </summary>
public sealed class CloudBatch : Entity
{
    public required Guid VaultId { get; init; }

    public required string PlayerUuid { get; init; }

    public required Guid ServerId { get; init; }

    public required long Epoch { get; init; }

    public required long Seq { get; init; }

    /// <summary>SHA-256 hex do corpo canônico do lote.</summary>
    public required string PayloadHash { get; init; }

    public CloudBatchStatus Status { get; private set; } = CloudBatchStatus.Applied;

    public void MarkQuarantined()
    {
        Status = CloudBatchStatus.Quarantined;
        Touch();
    }

    /// <summary>Estornado por um incidente de rollback: o mundo voltou para antes dele.</summary>
    public void MarkReverted()
    {
        if (Status != CloudBatchStatus.Applied)
            throw new InvalidOperationException("Só lote aplicado é estornado.");
        Status = CloudBatchStatus.Reverted;
        Touch();
    }

    public void MarkResolved(CloudBatchStatus status)
    {
        if (Status != CloudBatchStatus.Quarantined)
            throw new InvalidOperationException("Só lote em quarentena é resolvido.");
        if (status is not (CloudBatchStatus.Applied or CloudBatchStatus.Discarded))
            throw new ArgumentOutOfRangeException(nameof(status));
        Status = status;
        Touch();
    }
}

public enum CloudBatchStatus
{
    Applied,
    Quarantined,
    Discarded,

    /// <summary>Estornado por um incidente de rollback (fatia do painel).</summary>
    Reverted
}
