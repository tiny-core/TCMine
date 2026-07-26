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
            // EnsureCreated (dentro do Start) materializa a pasta e cria o
            // container se preciso; depois liga. É idempotente.
            await orchestrator.StartAsync(serverId, ct);

            // Reconcilia a coluna com o Docker (fonte da verdade).
            server.Status = await orchestrator.GetStatusAsync(serverId, ct);
            server.UpdatedAt = DateTimeOffset.UtcNow;
            await servers.UpdateAsync(server, ct);

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