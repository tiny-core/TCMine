using TCMine.Contracts.Hubs;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Abstractions;

public interface IServerRepository
{
    /// <summary>Todos os servidores, de todos os modpacks — usado pelo painel.</summary>
    Task<IReadOnlyList<GameServer>> ListAllAsync(CancellationToken ct);

    Task<IReadOnlyList<GameServer>> ListByModpackAsync(Guid modpackId, CancellationToken ct);
    Task<GameServer?> GetByIdAsync(Guid id, CancellationToken ct);
    [Invalidates(ResourceKind.Servers)]
    Task AddAsync(GameServer server, CancellationToken ct);
    [Invalidates(ResourceKind.Servers)]
    Task UpdateAsync(GameServer server, CancellationToken ct);
    [Invalidates(ResourceKind.Servers)]
    Task RemoveAsync(Guid id, CancellationToken ct);

    /// <summary>Snapshots de um servidor, do mais novo para o mais antigo.</summary>
    Task<IReadOnlyList<WorldBackup>> ListBackupsAsync(Guid gameServerId, CancellationToken ct);

    /// <summary>
    ///     Quantidade e bytes totais de TODOS os backups, de todos os
    ///     servidores — agregado no banco numa consulta só. A tela de storage só
    ///     quer o total; buscar backup por backup, um servidor de cada vez, seria
    ///     N consultas para somar um número que o próprio banco já sabe somar.
    /// </summary>
    Task<(int Count, long TotalBytes)> GetBackupUsageAsync(CancellationToken ct);

    Task<WorldBackup?> GetBackupAsync(Guid backupId, CancellationToken ct);

    [NotVisibleToLauncher("Backups de mundo só aparecem no painel.")]
    Task AddBackupAsync(WorldBackup backup, CancellationToken ct);

    [NotVisibleToLauncher("Backups de mundo só aparecem no painel.")]
    Task RemoveBackupAsync(Guid backupId, CancellationToken ct);
}
