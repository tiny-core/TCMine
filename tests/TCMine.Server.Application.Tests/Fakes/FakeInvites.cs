using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Security;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Tests.Fakes;

/// <summary>
///     Convites em memória. Guarda as instâncias recebidas, então o que o caso
///     de uso alterar fica visível para o teste sem espiar o UpdateAsync.
/// </summary>
internal sealed class FakeInvites(params Invite[] seed) : IInviteRepository
{
    private readonly List<Invite> _invites = [.. seed];

    public Invite? Adicionado { get; private set; }

    public Task AddAsync(Invite invite, CancellationToken ct)
    {
        Adicionado = invite;
        _invites.Add(invite);
        return Task.CompletedTask;
    }

    public Task<Invite?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_invites.FirstOrDefault(i => i.Id == id));

    public Task<Invite?> GetByCodeHashAsync(string codeHash, CancellationToken ct) =>
        Task.FromResult(_invites.FirstOrDefault(i => i.CodeHash == codeHash));

    public Task<IReadOnlyList<Invite>> ListByServerAsync(Guid gameServerId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Invite>>(
            [.. _invites.Where(i => i.GameServerId == gameServerId)]);

    public Task UpdateAsync(Invite invite, CancellationToken ct) => Task.CompletedTask;
}

internal sealed class FakeMemberships(params Membership[] seed) : IMembershipRepository
{
    private readonly List<Membership> _memberships = [.. seed];

    public Membership? Adicionado { get; private set; }
    public Guid? Removido { get; private set; }

    public Task AddAsync(Membership membership, CancellationToken ct)
    {
        Adicionado = membership;
        _memberships.Add(membership);
        return Task.CompletedTask;
    }

    public Task<Membership?> GetAsync(Guid userId, Guid gameServerId, CancellationToken ct) =>
        Task.FromResult(_memberships.FirstOrDefault(m =>
            m.UserId == userId && m.GameServerId == gameServerId));

    public Task<IReadOnlyList<Membership>> ListByServerAsync(Guid gameServerId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Membership>>(
            [.. _memberships.Where(m => m.GameServerId == gameServerId)]);

    public Task<IReadOnlyList<Membership>> ListByUserAsync(Guid userId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Membership>>(
            [.. _memberships.Where(m => m.UserId == userId)]);

    public Task<IReadOnlyList<ServerMemberView>> ListWithUsersAsync(
        Guid gameServerId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ServerMemberView>>(
        [
            .. _memberships
                .Where(m => m.GameServerId == gameServerId)
                .Select(m => new ServerMemberView(
                    m.Id, m.UserId, $"usuario-{m.UserId:N}"[..16], null, m.Role.ToDto(), null))
        ]);

    public Task UpdateAsync(Membership membership, CancellationToken ct) => Task.CompletedTask;

    public Task RemoveAsync(Guid id, CancellationToken ct)
    {
        Removido = id;
        _memberships.RemoveAll(m => m.Id == id);
        return Task.CompletedTask;
    }
}
