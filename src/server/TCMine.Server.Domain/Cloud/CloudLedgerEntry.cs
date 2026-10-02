using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Cloud;

/// <summary>
///     Uma linha do histórico de um saldo — append-only, é a verdade da nuvem.
///     Não há método que altere uma linha: correção do admin, estorno de
///     rollback e aplicação de quarentena são linhas NOVAS, com origem e motivo.
///     É isso que permite investigar duplicação e auditar o próprio admin.
/// </summary>
public sealed class CloudLedgerEntry : Entity
{
    public required Guid ChannelId { get; init; }

    public required Guid ItemTypeId { get; init; }

    public required long Delta { get; init; }

    public required long BalanceAfter { get; init; }

    public required CloudLedgerSource Source { get; init; }

    public Guid? BatchId { get; init; }

    public Guid? ActorUserId { get; init; }

    public string? Reason { get; init; }
}

public enum CloudLedgerSource
{
    /// <summary>Lote de um servidor de jogo.</summary>
    Game,

    /// <summary>Ajuste manual no painel (motivo obrigatório).</summary>
    Admin,

    /// <summary>Estorno de rollback de mundo.</summary>
    Revert,

    /// <summary>Lote de quarentena aplicado pelo admin.</summary>
    QuarantineApply
}
