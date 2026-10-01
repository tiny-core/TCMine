using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Servers;

/// <summary>
///     Um jogador pedindo acesso a um servidor com whitelist.
///     O caminho inverso do convite: o convite é o dono empurrando um código; o
///     pedido é o jogador, vendo o servidor na lista, puxando. Os dois convivem
///     — um não substitui o outro.
/// </summary>
public sealed class AccessRequest : Entity
{
    public required Guid UserId { get; set; }
    public required Guid GameServerId { get; set; }

    public AccessRequestStatus Status { get; private set; } = AccessRequestStatus.Pending;

    public DateTimeOffset? ResolvedAt { get; private set; }

    public Guid? ResolvedByUserId { get; private set; }

    /// <summary>
    ///     Concede: vira Membership em <c>ApproveAccessRequest</c>, não aqui — o
    ///     pedido só registra a decisão e quem a tomou.
    /// </summary>
    public void Approve(Guid resolvedByUserId, DateTimeOffset now)
    {
        if (Status is not AccessRequestStatus.Pending)
            throw new InvalidOperationException("Pedido já foi resolvido.");

        Status = AccessRequestStatus.Approved;
        ResolvedByUserId = resolvedByUserId;
        ResolvedAt = now;
        Touch();
    }

    public void Deny(Guid resolvedByUserId, DateTimeOffset now)
    {
        if (Status is not AccessRequestStatus.Pending)
            throw new InvalidOperationException("Pedido já foi resolvido.");

        Status = AccessRequestStatus.Denied;
        ResolvedByUserId = resolvedByUserId;
        ResolvedAt = now;
        Touch();
    }
}

public enum AccessRequestStatus
{
    Pending,
    Approved,
    Denied
}
