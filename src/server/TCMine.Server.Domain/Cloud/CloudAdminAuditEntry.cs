using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Cloud;

/// <summary>
///     Uma ação de alguém no painel da nuvem (ligar servidor, revogar chave,
///     descongelar, aplicar quarentena...). Append-only como o ledger: o ledger
///     diz o que mudou nos saldos; isto diz quem decidiu o quê.
/// </summary>
public sealed class CloudAdminAuditEntry : Entity
{
    public required Guid VaultId { get; init; }

    public Guid? ActorUserId { get; init; }

    /// <summary>Verbo curto e estável (ex.: "channel.unfreeze"), para filtrar.</summary>
    public required string Action { get; init; }

    public required string Details { get; init; }
}
