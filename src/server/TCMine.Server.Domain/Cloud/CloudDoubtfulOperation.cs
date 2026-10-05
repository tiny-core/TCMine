using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Cloud;

/// <summary>
///     Operação que um servidor de jogo pode ter perdido numa queda (o mod não
///     sabe se o mundo gravou o item). Nunca é devolvida sozinha: devolver às
///     cegas duplicaria quando o mundo já tinha gravado. O dono decide.
///     (ReportId, Index) identifica a linha no relatório do mod: reenviar o mesmo
///     relatório depois de uma falha de rede não duplica a fila.
/// </summary>
public sealed class CloudDoubtfulOperation : Entity
{
    public required Guid VaultId { get; init; }

    public required Guid ServerId { get; init; }

    public required string ReportId { get; init; }

    public required int Index { get; init; }

    public required string PlayerUuid { get; init; }

    public required Guid ChannelId { get; init; }

    public required string Fingerprint { get; init; }

    public required CloudDoubtfulKind Kind { get; init; }

    public required long Amount { get; init; }

    public CloudDoubtfulStatus Status { get; private set; } = CloudDoubtfulStatus.Open;

    public DateTimeOffset? ResolvedAt { get; private set; }

    public Guid? ResolvedByUserId { get; private set; }

    public bool IsOpen => Status == CloudDoubtfulStatus.Open;

    public void Resolve(CloudDoubtfulStatus status, Guid userId, DateTimeOffset now)
    {
        if (!IsOpen) throw new InvalidOperationException("Operação já resolvida.");
        if (status == CloudDoubtfulStatus.Open) throw new ArgumentOutOfRangeException(nameof(status));
        Status = status;
        ResolvedByUserId = userId;
        ResolvedAt = now;
        Touch();
    }
}

public enum CloudDoubtfulKind
{
    /// <summary>Saiu do mundo para a nuvem, mas o crédito não chegou a ser durável.</summary>
    PendingCredit,

    /// <summary>Saiu da nuvem depois do último save; talvez não chegou ao disco do mundo.</summary>
    RecentDebit
}

public enum CloudDoubtfulStatus
{
    Open,
    Refunded,
    Dismissed
}
