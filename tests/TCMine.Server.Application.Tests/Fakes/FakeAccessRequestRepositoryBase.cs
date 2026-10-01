using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Tests.Fakes;

/// <summary>
///     Base para os fakes de <see cref="IAccessRequestRepository" />: implementa
///     tudo lançando e deixa cada teste sobrescrever só o que exercita. Mesmo
///     motivo da <see cref="FakeServerRepositoryBase" /> — sem ela, um membro
///     novo na porta quebra todos os fakes de uma vez, ruído puro.
/// </summary>
public abstract class FakeAccessRequestRepositoryBase : IAccessRequestRepository
{
    public virtual Task AddAsync(AccessRequest request, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task<AccessRequest?> GetByIdAsync(Guid id, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task<AccessRequest?> GetPendingAsync(Guid userId, Guid gameServerId, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task<IReadOnlyList<AccessRequest>> ListPendingByUserAsync(Guid userId, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task<IReadOnlyList<AccessRequestView>> ListPendingForServersAsync(
        IReadOnlyList<Guid> gameServerIds, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task UpdateAsync(AccessRequest request, CancellationToken ct) =>
        throw new NotImplementedException();
}
