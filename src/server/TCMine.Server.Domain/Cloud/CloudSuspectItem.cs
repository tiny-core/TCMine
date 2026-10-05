using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Cloud;

/// <summary>
///     Item que um servidor de jogo recusou por parecer guardar dados no MUNDO
///     (heurística do mod: UUID nos componentes). Fica na fila do dono, que
///     decide uma vez: permitir (vira regra Allow) ou bloquear de vez (regra
///     Block). Uma linha por item por nuvem, somando as tentativas.
/// </summary>
public sealed class CloudSuspectItem : Entity
{
    public required Guid VaultId { get; init; }

    public required string ItemId { get; init; }

    /// <summary>Onde o sinal foi achado (ex.: "storage_uuid (nome da chave)"), o último relatado.</summary>
    public string Evidence { get; private set; } = "";

    public long Attempts { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    public CloudSuspectStatus Status { get; private set; } = CloudSuspectStatus.Pending;

    public Guid? ResolvedByUserId { get; private set; }

    public void Seen(long attempts, string evidence, DateTimeOffset now)
    {
        Attempts = checked(Attempts + Math.Max(1, attempts));
        Evidence = evidence.Length > 256 ? evidence[..256] : evidence;
        LastSeenAt = now;
        Touch();
    }

    public void Resolve(CloudSuspectStatus status, Guid userId)
    {
        if (status == CloudSuspectStatus.Pending)
            throw new ArgumentOutOfRangeException(nameof(status));
        Status = status;
        ResolvedByUserId = userId;
        Touch();
    }
}

public enum CloudSuspectStatus
{
    Pending,
    Allowed,
    Blocked
}
