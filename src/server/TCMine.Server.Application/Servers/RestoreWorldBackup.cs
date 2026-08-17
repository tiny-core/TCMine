using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Security;

namespace TCMine.Server.Application.Servers;

/// <summary>
///     Repõe o mundo a partir de um snapshot.
///     Duas guardas. A primeira é a mesma do backup: servidor parado, senão o
///     jogo escreve por cima do que estamos extraindo. A segunda é a que
///     realmente importa — restaurar um mundo salvo numa versão do modpack para
///     dentro de OUTRA versão é o mesmo problema que motivou o backup, ao
///     contrário. Aqui não bloqueamos, mas o caso de uso devolve o aviso para a
///     tela poder exigir confirmação explícita.
/// </summary>
public sealed class RestoreWorldBackup(
    IServerRepository servers,
    IServerOrchestrator orchestrator,
    IWorldBackupStore store,
    IJobProgressReporter progress,
    ICurrentUserScope scope)
{
    public async Task<Result> HandleAsync(
        Guid backupId, CancellationToken ct, bool acceptVersionMismatch = false, Guid jobId = default)
    {
        // Como no DeleteWorldBackup: o snapshot é quem diz o servidor, então a
        // autorização vem logo depois de encontrá-lo.
        var backup = await servers.GetBackupAsync(backupId, ct);
        if (backup is null)
            return Result.Fail("Backup não encontrado.");

        var auth = await scope.RequireAsync(backup.GameServerId, ServerAccessPolicy.CanAccessBackups, ct);
        if (!auth.Succeeded)
            return Result.Fail("Backup não encontrado.");

        var server = await servers.GetByIdAsync(backup.GameServerId, ct);
        if (server is null)
            return Result.Fail("Servidor não encontrado.");

        var status = await orchestrator.GetStatusAsync(server.Id, ct);
        if (status is not (GameServerStatus.Stopped or GameServerStatus.Crashed))
            return Result.Fail("Pare o servidor antes de restaurar o mundo.");

        // Snapshot de outra versão: os mods que geraram esse mundo podem não ser
        // os que estão fixados agora.
        if (!acceptVersionMismatch
            && backup.ModpackVersionId is { } origem
            && origem != server.ModpackVersionId)
        {
            return Result.Fail(
                $"Este backup é da versão {backup.ModpackVersionLabel ?? "anterior"}, e o servidor está "
                + "em outra. Restaurar assim pode quebrar o mundo — confirme se é isso mesmo que quer.");
        }

        void Report(int done, int total) =>
            progress.Report(jobId, new JobProgress(
                $"Restaurando o mundo — {server.Name}", "Extraindo", done, total));

        try
        {
            var ok = await store.RestoreAsync(
                server.Id, backup.FileName, jobId == default ? null : Report, ct);

            if (!ok)
            {
                progress.Complete(jobId, "Arquivo do backup não está mais no disco.");
                return Result.Fail("O arquivo deste backup não está mais no disco.");
            }

            progress.Complete(jobId);
            return Result.Success();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            progress.Complete(jobId, ex.Message);
            return Result.Fail($"Falha ao restaurar: {ex.Message}");
        }
    }
}
