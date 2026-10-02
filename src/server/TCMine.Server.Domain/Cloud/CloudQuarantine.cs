using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Cloud;

/// <summary>
///     Lote recusado, guardado inteiro para o dono decidir no painel. Nunca é
///     aplicado sozinho: chegar aqui significa que algo saiu do esperado (época
///     velha, saldo negativo, divergência) e só um humano sabe se aplicar
///     corrige ou duplica.
/// </summary>
public sealed class CloudQuarantine : Entity
{
    public required Guid VaultId { get; init; }

    public required Guid BatchId { get; init; }

    public required CloudQuarantineReason Reason { get; init; }

    /// <summary>Explicação legível (qual saldo, qual época...).</summary>
    public required string Detail { get; init; }

    /// <summary>O lote como chegou (JSON), para o painel mostrar e reaplicar.</summary>
    public required string PayloadJson { get; init; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public Guid? ResolvedByUserId { get; private set; }

    public CloudQuarantineResolution? Resolution { get; private set; }

    public bool IsOpen => ResolvedAt is null;

    public void Resolve(CloudQuarantineResolution resolution, Guid userId, DateTimeOffset now)
    {
        if (!IsOpen) throw new InvalidOperationException("Quarentena já resolvida.");
        Resolution = resolution;
        ResolvedByUserId = userId;
        ResolvedAt = now;
        Touch();
    }
}

public enum CloudQuarantineReason
{
    StaleEpoch,
    UnknownEpoch,
    NotHolder,
    SequenceGap,
    NegativeBalance,
    Divergence,
    QuotaExceeded,
    UnknownChannel,
    UnknownItem,
    FrozenChannel,
    PayloadMismatch
}

public enum CloudQuarantineResolution
{
    Applied,
    Discarded
}
