using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Security;

namespace TCMine.Server.Application.Servers;

public sealed class StopGameServer(
    IServerOrchestrator orchestrator,
    IServerRepository servers,
    IJobProgressReporter progress,
    ICurrentUserScope scope)
{
    // Timeout generoso: o stop-server.sh do itzg salva o mundo antes de sair.
    // Matar antes disso corrompe chunks — por isso 60s, não 10.
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(60);

    public async Task<Result> HandleAsync(Guid serverId, CancellationToken ct, Guid jobId = default)
    {
        var auth = await scope.RequireAsync(serverId, ServerAccessPolicy.CanControlPower, ct);
        if (!auth.Succeeded)
            return auth;

        var server = await servers.GetByIdAsync(serverId, ct);
        if (server is null)
            return Result.Fail("Servidor não encontrado.");

        void Report(string step)
        {
            if (jobId != default)
                progress.Report(jobId, new JobProgress($"Parando {server.Name}", step));
        }

        try
        {
            // Pode levar até um minuto: o servidor salva o mundo antes de sair, e
            // é justamente esse tempo que não se pode cortar.
            Report("Salvando o mundo e desligando…");
            await orchestrator.StopAsync(serverId, StopTimeout, ct);

            server.Status = await orchestrator.GetStatusAsync(serverId, ct);
            server.UpdatedAt = DateTimeOffset.UtcNow;
            await servers.UpdateAsync(server, ct);

            progress.Complete(jobId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            progress.Complete(jobId, ex.Message);
            return Result.Fail($"Falha ao parar: {ex.Message}");
        }
    }
}
