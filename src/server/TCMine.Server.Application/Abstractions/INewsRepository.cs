using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Abstractions;

public interface INewsRepository
{
    Task<IReadOnlyList<News>> ListByModpackAsync(Guid modpackId, CancellationToken ct);
    Task<News?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(News news, CancellationToken ct);
    Task UpdateAsync(News news, CancellationToken ct);
    Task RemoveAsync(Guid id, CancellationToken ct);
}