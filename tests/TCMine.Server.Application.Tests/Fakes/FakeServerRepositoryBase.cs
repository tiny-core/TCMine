using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Tests.Fakes;

/// <summary>
///     Base para os fakes de <see cref="IServerRepository" />: implementa tudo
///     lançando e deixa cada teste sobrescrever só o que exercita.
///     Mesmo motivo da <see cref="FakeModpackRepositoryBase" /> — sem ela, um
///     método novo na porta quebra todos os fakes de uma vez, ruído puro.
/// </summary>
public abstract class FakeServerRepositoryBase : IServerRepository
{
    public virtual Task<IReadOnlyList<GameServer>> ListAllAsync(CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task<IReadOnlyList<GameServer>> ListByModpackAsync(Guid modpackId, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task<GameServer?> GetByIdAsync(Guid id, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task AddAsync(GameServer server, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task<bool> UpdateAsync(GameServer server, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task UpdateStatusAsync(Guid id, GameServerStatus status, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task SetContainerIdAsync(Guid id, string? containerId, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task SetPinnedVersionAsync(Guid id, Guid modpackVersionId, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task SetCloudVaultAsync(Guid id, Guid? cloudVaultId, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task<bool> MarkWorldInitializedAsync(Guid id, DateTimeOffset at, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task RemoveAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();

    public virtual Task<IReadOnlyList<WorldBackup>> ListBackupsAsync(Guid gameServerId, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task<(int Count, long TotalBytes)> GetBackupUsageAsync(CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task<WorldBackup?> GetBackupAsync(Guid backupId, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task AddBackupAsync(WorldBackup backup, CancellationToken ct) =>
        throw new NotImplementedException();

    public virtual Task RemoveBackupAsync(Guid backupId, CancellationToken ct) =>
        throw new NotImplementedException();
}
