using TCMine.Contracts.Modpacks;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Servers;

/// <summary>
///     Troca a versão fixada de um servidor.
///     Sem mundo é só re-apontar: os bytes da nova versão já estão no blob store
///     e a materialização acontece no próximo start.
///     COM mundo, trocar mods pode corromper o save (mod removido = registry
///     faltando; downgrade = formato de dados incompatível). Antes isso era
///     bloqueado; agora é permitido desde que se tire um snapshot primeiro — a
///     operação deixa de ser irreversível e passa a ser apenas demorada. E o
///     snapshot exige servidor parado, o que também impede trocar a versão
///     debaixo de quem está jogando.
/// </summary>
public sealed class ChangeServerVersion(
    IServerRepository servers,
    IModpackRepository modpacks,
    IServerOrchestrator orchestrator,
    CreateWorldBackup backup)
{
    public async Task<Result> HandleAsync(
        Guid serverId, Guid targetVersionId, CancellationToken ct, Guid jobId = default)
    {
        var server = await servers.GetByIdAsync(serverId, ct);
        if (server is null)
            return Result.Fail("Servidor não encontrado.");

        if (server.ModpackVersionId == targetVersionId)
            return Result.Fail("O servidor já está nesta versão.");

        // A versão alvo tem de ser do mesmo modpack e instalável (publicada ou
        // arquivada). Draft/Resolving/Failed não servem para rodar.
        var target = await modpacks.GetVersionAsync(targetVersionId, ct);
        if (target is null || target.ModpackId != server.ModpackId)
            return Result.Fail("Versão inválida para este modpack.");

        if (target.State is not (ModpackVersionState.Ready or ModpackVersionState.Archived) || target.IsPreRelease)
            return Result.Fail("Só é possível fixar uma versão estável publicada ou arquivada.");

        if (server.HasWorld)
        {
            var status = await orchestrator.GetStatusAsync(serverId, ct);
            if (status is not (GameServerStatus.Stopped or GameServerStatus.Crashed))
            {
                return Result.Fail(
                    "Pare o servidor antes de trocar a versão: é preciso salvar o mundo primeiro, "
                    + "e trocar mods debaixo de quem está jogando quebra a sessão.");
            }

            // Backup ANTES de mexer no ponteiro. Se falhar, nada muda — é o que
            // torna a troca reversível.
            var snapshot = await backup.HandleAsync(
                serverId,
                $"Automático, antes de mudar para {target.Version}",
                ct,
                WorldBackupReason.BeforeVersionChange,
                jobId);

            if (!snapshot.Succeeded)
                return Result.Fail($"A troca foi cancelada: não deu para salvar o mundo. {snapshot.Error}");
        }

        server.ModpackVersionId = targetVersionId;
        server.UpdatedAt = DateTimeOffset.UtcNow;

        await servers.UpdateAsync(server, ct);
        return Result.Success();
    }
}
