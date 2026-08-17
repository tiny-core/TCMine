using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Abstractions;

public interface IInviteRepository
{
    Task AddAsync(Invite invite, CancellationToken ct);

    Task<Invite?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>
    ///     Busca pelo hash do código apresentado. É a única forma de encontrar um
    ///     convite pelo que o usuário digitou — o valor em claro não existe no
    ///     banco.
    /// </summary>
    Task<Invite?> GetByCodeHashAsync(string codeHash, CancellationToken ct);

    /// <summary>Convites de um servidor, mais recentes primeiro (ordem de Id).</summary>
    Task<IReadOnlyList<Invite>> ListByServerAsync(Guid gameServerId, CancellationToken ct);

    Task UpdateAsync(Invite invite, CancellationToken ct);
}

public interface IMembershipRepository
{
    Task AddAsync(Membership membership, CancellationToken ct);

    Task<Membership?> GetAsync(Guid userId, Guid gameServerId, CancellationToken ct);

    Task<IReadOnlyList<Membership>> ListByServerAsync(Guid gameServerId, CancellationToken ct);

    Task UpdateAsync(Membership membership, CancellationToken ct);

    Task RemoveAsync(Guid id, CancellationToken ct);
}
