using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Servers;

public sealed class StopGameServer(
    IServerOrchestrator orchestrator,
    IServerRepository servers)
{
    // Timeout generoso: o stop-server.sh do itzg salva o mundo antes de sair.
    // Matar antes disso corrompe chunks — por isso 60s, não 10.
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(60);

    public async Task<Result> HandleAsync(Guid serverId, CancellationToken ct)
    {
        var server = await servers.GetByIdAsync(serverId, ct);
        if (server is null)
            return Result.Fail("Servidor não encontrado.");

        try
        {
            // EnsureCreated (dentro do Start) materializa a pasta, cria o
            // container e persiste o ContainerId. É idempotente.
            await orchestrator.StartAsync(serverId, ct);

            // Recarrega: o StartAsync gravou o ContainerId numa instância própria.
            // A nossa cópia 'server' está velha (ContainerId ainda null) — gravar
            // por cima dela apagaria o Id recém-persistido, porque Update marca
            // todas as colunas.
            var fresh = await servers.GetByIdAsync(serverId, ct);
            if (fresh is null)
                return Result.Fail("Servidor não encontrado após iniciar.");

            fresh.Status = await orchestrator.GetStatusAsync(serverId, ct);
            fresh.UpdatedAt = DateTimeOffset.UtcNow;
            await servers.UpdateAsync(fresh, ct);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Falha ao parar: {ex.Message}");
        }
    }
}
