using TCMine.Contracts.Hubs;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Abstractions;

public interface INewsRepository
{
    Task<IReadOnlyList<News>> ListByModpackAsync(Guid modpackId, CancellationToken ct);
    Task<News?> GetByIdAsync(Guid id, CancellationToken ct);
    [Invalidates(ResourceKind.News)]
    Task AddAsync(News news, CancellationToken ct);
    [Invalidates(ResourceKind.News)]
    Task UpdateAsync(News news, CancellationToken ct);
    [Invalidates(ResourceKind.News)]
    Task RemoveAsync(Guid id, CancellationToken ct);
}
