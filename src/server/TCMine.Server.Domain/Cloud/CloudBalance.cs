using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Cloud;

/// <summary>
///     Quanto de um item há num canal. É DERIVADO do ledger
///     (<see cref="CloudLedgerEntry" />) e atualizado na mesma transação dele; a
///     verdade é o ledger. Nunca negativo: um débito que deixaria negativo é bug
///     ou trapaça, e o lote inteiro vai para a quarentena antes de chegar aqui.
/// </summary>
public sealed class CloudBalance : Entity
{
    public required Guid ChannelId { get; init; }

    public required Guid ItemTypeId { get; init; }

    public long Amount { get; private set; }

    /// <summary>Aplica uma variação e devolve o saldo novo.</summary>
    public long Apply(long delta)
    {
        var after = checked(Amount + delta);
        if (after < 0)
            throw new InvalidOperationException($"Saldo negativo no canal {ChannelId}: {after}.");
        Amount = after;
        Touch();
        return after;
    }
}
