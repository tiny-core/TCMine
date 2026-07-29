using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Servers;

public sealed class DeleteGameServer(
    IServerRepository servers,
    IServerOrchestrator orchestrator,
    IInstanceMaterializer materializer)
{
    public async Task<Result> HandleAsync(Guid id, CancellationToken ct)
    {
        var server = await servers.GetByIdAsync(id, ct);
        if (server is null)
            return Result.Fail("Servidor não encontrado.");

        try
        {
            await orchestrator.RemoveAsync(id, ct); // 1. para e remove o container
            await materializer.DeleteInstanceAsync(id, ct); // 2. apaga mods, configs e mundo
        }
        catch (Exception ex)
        {
            // Se algo falhar aqui, não apagamos a linha — o admin vê o erro e
            // repete, em vez de ficar com container/pasta órfãos e sem registo.
            return Result.Fail($"Não foi possível remover a instância: {ex.Message}");
        }

        await servers.RemoveAsync(id, ct); // 3. remove o registo
        return Result.Success();
    }
}