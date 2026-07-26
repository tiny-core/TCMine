using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Servers;

public sealed class StartGameServer(
    IServerOrchestrator orchestrator,
    IServerRepository servers)
{
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
            // por cima dela apagaria o ID recém-persistido, porque Update marca
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
            // Docker fora do ar, imagem a puxar, porta ocupada… o admin precisa
            // de ver a causa, não um erro genérico.
            return Result.Fail($"Falha ao iniciar: {ex.Message}");
        }
    }
}