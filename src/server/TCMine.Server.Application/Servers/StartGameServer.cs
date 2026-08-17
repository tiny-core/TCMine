using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Security;

namespace TCMine.Server.Application.Servers;

public sealed class StartGameServer(
    IServerOrchestrator orchestrator,
    IServerRepository servers,
    IJobProgressReporter progress,
    ICurrentUserScope scope)
{
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
                progress.Report(jobId, new JobProgress($"Iniciando {server.Name}", step));
        }

        try
        {
            // O primeiro start de um modpack grande é longo: materializa a pasta
            // (hardlink de centenas de jars) e pode ter de puxar a imagem do
            // itzg. Sem dizer isso, parece que o botão não funcionou.
            Report("Preparando a instância e o container…");
            // EnsureCreated (dentro do Start) materializa a pasta, cria o
            // container e persiste o ContainerId. É idempotente.
            await orchestrator.StartAsync(serverId, ct);

            // Recarrega: o StartAsync gravou o ContainerId numa instância própria.
            // A nossa cópia 'server' está velha (ContainerId ainda null) — gravar
            // por cima dela apagaria o ID recém-persistido, porque Update marca
            // todas as colunas.
            var fresh = await servers.GetByIdAsync(serverId, ct);
            if (fresh is null)
                return Result.Fail("Servidor não encontrado após iniciar.");

            Report("Conferindo o estado do container…");
            fresh.Status = await orchestrator.GetStatusAsync(serverId, ct);
            fresh.UpdatedAt = DateTimeOffset.UtcNow;
            await servers.UpdateAsync(fresh, ct);

            progress.Complete(jobId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            // Docker fora do ar, imagem a puxar, porta ocupada… o admin precisa
            // de ver a causa, não um erro genérico.
            progress.Complete(jobId, ex.Message);
            return Result.Fail($"Falha ao iniciar: {ex.Message}");
        }
    }
}
