using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Security;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Servers;

/// <summary>
///     Tira um instantâneo do mundo.
///     Com o servidor parado é uma cópia direta. Com ele NO AR, faz o que os
///     painéis maduros fazem: <c>save-off</c> para o jogo parar de escrever,
///     <c>save-all flush</c> para descarregar o que está em memória, copia, e
///     <c>save-on</c> de volta. Sem isso, metade dos chunks viria de antes da
///     cópia e metade de depois — um .zip íntegro que não abre.
///     O <c>save-on</c> roda em finally, sempre. Deixá-lo desligado por um erro
///     no meio seria pior que não ter backup: o servidor rodaria sem persistir
///     nada, e a próxima queda levaria tudo desde então.
/// </summary>
public sealed class CreateWorldBackup(
    IServerRepository servers,
    IServerOrchestrator orchestrator,
    IRconClient rcon,
    IWorldBackupStore store,
    IModpackRepository modpacks,
    ISettingsRepository settings,
    IJobProgressReporter progress,
    ICurrentUserScope scope)
{
    public async Task<Result<Guid>> HandleAsync(
        Guid serverId, string? note, CancellationToken ct,
        WorldBackupReason reason = WorldBackupReason.Manual, Guid jobId = default)
    {
        var auth = await scope.RequireAsync(serverId, ServerAccessPolicy.CanAccessBackups, ct);
        if (!auth.Succeeded)
            return Result<Guid>.Fail(auth.Error!);

        var server = await servers.GetByIdAsync(serverId, ct);
        if (server is null)
            return Result<Guid>.Fail("Servidor não encontrado.");

        // O container é a verdade, não a coluna: o admin pode ter subido o
        // servidor por fora do painel.
        var status = await orchestrator.GetStatusAsync(serverId, ct);
        var aQuente = status is GameServerStatus.Running;

        if (!aQuente && status is not (GameServerStatus.Stopped or GameServerStatus.Crashed))
        {
            // Iniciando ou parando: o estado é transitório e nenhum dos dois
            // caminhos vale. Esperar o servidor assentar é mais barato que um
            // backup duvidoso.
            return Result<Guid>.Fail(
                $"O servidor está {status}. Espere ele assentar antes de salvar o mundo.");
        }

        void Report(int done, int total) =>
            progress.Report(jobId, new JobProgress(
                $"Backup do mundo — {server.Name}", "Compactando", done, total));

        var autosaveDesligado = false;

        try
        {
            if (aQuente)
            {
                progress.Report(jobId, new JobProgress(
                    $"Backup do mundo — {server.Name}", "Pausando o autosave e descarregando o mundo"));

                // save-off primeiro: sem ele, o save-all seguinte não adiantaria
                // nada, porque o jogo voltaria a escrever no instante seguinte.
                await rcon.ExecuteAsync(serverId, "save-off", ct);
                autosaveDesligado = true;

                // flush força a gravação síncrona do que está em memória.
                await rcon.ExecuteAsync(serverId, "save-all flush", ct);
            }

            var stored = await store.CreateAsync(serverId, jobId == default ? null : Report, ct);
            if (stored is null)
            {
                progress.Complete(jobId, "Este servidor ainda não gerou mundo.");
                return Result<Guid>.Fail("Este servidor ainda não gerou mundo — não há o que salvar.");
            }

            var version = await modpacks.GetVersionAsync(server.ModpackVersionId, ct);

            var backup = new WorldBackup
            {
                GameServerId = serverId,
                FileName = stored.FileName,
                SizeBytes = stored.SizeBytes,
                Reason = reason,
                ModpackVersionId = server.ModpackVersionId,
                ModpackVersionLabel = version?.Version,
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                TakenHot = aQuente
            };

            await servers.AddBackupAsync(backup, ct);
            await PruneAsync(serverId, ct);

            progress.Complete(jobId);
            return Result<Guid>.Success(backup.Id);
        }
        catch (RconUnavailableException ex)
        {
            // Não deu para falar com o jogo: copiar assim seria copiar às cegas.
            progress.Complete(jobId, ex.Message);
            return Result<Guid>.Fail(
                $"O servidor está no ar mas não respondeu ao comando de salvar ({ex.Message}). "
                + "Pare o servidor e tente de novo.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            progress.Complete(jobId, ex.Message);
            return Result<Guid>.Fail($"Falha ao gravar o backup: {ex.Message}");
        }
        finally
        {
            if (autosaveDesligado)
                await RestoreAutosaveAsync(serverId, ct);
        }
    }

    /// <summary>
    ///     Religa o autosave, aconteça o que acontecer.
    ///     Se ESTA chamada falhar, o servidor fica rodando sem persistir — a
    ///     próxima queda levaria tudo desde agora. Por isso o erro é propagado em
    ///     vez de engolido: é a única falha deste caso de uso que exige ação
    ///     imediata do admin.
    /// </summary>
    private async Task RestoreAutosaveAsync(Guid serverId, CancellationToken ct)
    {
        try
        {
            await rcon.ExecuteAsync(serverId, "save-on", ct);
        }
        catch (RconUnavailableException ex)
        {
            throw new RconUnavailableException(
                "ATENÇÃO: o autosave do servidor ficou DESLIGADO e não foi possível religá-lo. "
                + "Rode 'save-on' pelo console ou reinicie o servidor — enquanto isso, nada do "
                + "que acontecer no jogo será gravado.",
                ex);
        }
    }

    /// <summary>
    ///     Apaga os automáticos que passaram do limite.
    ///     Roda depois de gravar, não num agendador: o único momento em que a
    ///     conta muda é quando um backup novo entra, e um serviço em background
    ///     só para isso seria maquinaria sem trabalho.
    ///     Manuais NUNCA expiram — foram um ato deliberado do admin.
    /// </summary>
    private async Task PruneAsync(Guid serverId, CancellationToken ct)
    {
        var config = await settings.GetAsync(ct);
        if (config.WorldBackupKeepCount <= 0)
            return; // ilimitado

        var automaticos = (await servers.ListBackupsAsync(serverId, ct))
            .Where(b => b.Reason is WorldBackupReason.BeforeVersionChange)
            .Skip(config.WorldBackupKeepCount)
            .ToList();

        foreach (var velho in automaticos)
        {
            // Arquivo primeiro: o inverso deixaria um .zip sem dono ocupando
            // disco e invisível ao painel.
            await store.DeleteAsync(serverId, velho.FileName, ct);
            await servers.RemoveBackupAsync(velho.Id, ct);
        }
    }
}
