using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Abstractions;

public interface IServerRepository
{
    Task<IReadOnlyList<GameServer>> ListByModpackAsync(Guid modpackId, CancellationToken ct);
    Task<GameServer?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(GameServer server, CancellationToken ct);
    Task UpdateAsync(GameServer server, CancellationToken ct);
    Task RemoveAsync(Guid id, CancellationToken ct);
}