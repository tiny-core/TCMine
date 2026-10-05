using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Cloud;

/// <summary>
///     Um mundo voltou no tempo (backup restaurado, cópia manual): o servidor de
///     jogo declarou no hello um checkpoint ANTERIOR a lotes que o TCMine já
///     aplicou. Os itens dos lotes "do futuro" podem estar de volta no mundo E
///     na nuvem. Enquanto aberto, a nuvem fica somente leitura naquele servidor;
///     o dono decide: estornar os lotes posteriores (recomendado) ou aceitar.
/// </summary>
public sealed class CloudRollbackIncident : Entity
{
    public required Guid VaultId { get; init; }

    public required Guid ServerId { get; init; }

    public required Guid WorldId { get; init; }

    /// <summary>Checkpoint que o servidor mandou (JSON), base do estorno.</summary>
    public required string CheckpointJson { get; init; }

    /// <summary>Resumo legível: quais jogadores, até onde o banco foi.</summary>
    public required string Detail { get; init; }

    public CloudIncidentStatus Status { get; private set; } = CloudIncidentStatus.Open;

    public DateTimeOffset? ResolvedAt { get; private set; }

    public Guid? ResolvedByUserId { get; private set; }

    public bool IsOpen => Status == CloudIncidentStatus.Open;

    public void Resolve(CloudIncidentStatus status, Guid userId, DateTimeOffset now)
    {
        if (!IsOpen) throw new InvalidOperationException("Incidente já resolvido.");
        if (status == CloudIncidentStatus.Open) throw new ArgumentOutOfRangeException(nameof(status));
        Status = status;
        ResolvedByUserId = userId;
        ResolvedAt = now;
        Touch();
    }
}

public enum CloudIncidentStatus
{
    Open,
    Reverted,
    Accepted
}
