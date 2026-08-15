using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Servers;

/// <summary>
///     Apaga um snapshot: o arquivo primeiro, o registro depois.
///     Nessa ordem porque o inverso deixaria um .zip sem dono ocupando disco e
///     invisível ao painel — lixo que ninguém encontra.
/// </summary>
public sealed class DeleteWorldBackup(IServerRepository servers, IWorldBackupStore store)
{
    public async Task<Result> HandleAsync(Guid backupId, CancellationToken ct)
    {
        var backup = await servers.GetBackupAsync(backupId, ct);
        if (backup is null)
            return Result.Fail("Backup não encontrado.");

        // Sumir do disco por fora não impede limpar o registro: o objetivo é
        // que os dois deixem de existir.
        await store.DeleteAsync(backup.GameServerId, backup.FileName, ct);
        await servers.RemoveBackupAsync(backupId, ct);

        return Result.Success();
    }
}
