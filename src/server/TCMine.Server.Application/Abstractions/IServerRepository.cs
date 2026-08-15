using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Abstractions;

public interface IServerRepository
{
    /// <summary>Todos os servidores, de todos os modpacks — usado pelo painel.</summary>
    Task<IReadOnlyList<GameServer>> ListAllAsync(CancellationToken ct);

    Task<IReadOnlyList<GameServer>> ListByModpackAsync(Guid modpackId, CancellationToken ct);
    Task<GameServer?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(GameServer server, CancellationToken ct);
    Task UpdateAsync(GameServer server, CancellationToken ct);
    Task RemoveAsync(Guid id, CancellationToken ct);

    /// <summary>Snapshots de um servidor, do mais novo para o mais antigo.</summary>
    Task<IReadOnlyList<WorldBackup>> ListBackupsAsync(Guid gameServerId, CancellationToken ct);

    Task<WorldBackup?> GetBackupAsync(Guid backupId, CancellationToken ct);

    Task AddBackupAsync(WorldBackup backup, CancellationToken ct);

    Task RemoveBackupAsync(Guid backupId, CancellationToken ct);
}
