using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Security;

namespace TCMine.Server.Application.Servers;

public sealed class DeleteGameServer(
    IServerRepository servers,
    IServerOrchestrator orchestrator,
    IInstanceMaterializer materializer,
    IJobProgressReporter progress,
    ICurrentUserScope scope)
{
    public async Task<Result> HandleAsync(Guid id, CancellationToken ct, Guid jobId = default)
    {
        var auth = await scope.RequireAsync(id, ServerAccessPolicy.CanDelete, ct);
        if (!auth.Succeeded)
            return auth;

        var server = await servers.GetByIdAsync(id, ct);
        if (server is null)
            return Result.Fail("Servidor não encontrado.");

        void Report(string step)
        {
            if (jobId != default)
                progress.Report(jobId, new JobProgress($"Removendo {server.Name}", step));
        }

        try
        {
            // Parar o container espera o mundo salvar (até um minuto) e apagar a
            // instância varre milhares de arquivos: sem dizer em que passo está,
            // a janela some e o admin fica olhando para nada.
            Report("Parando e removendo o container…");
            await orchestrator.RemoveAsync(id, ct); // 1. para e remove o container

            Report("Apagando mods, configs e mundo…");
            await materializer.DeleteInstanceAsync(id, ct); // 2. apaga mods, configs e mundo
        }
        catch (Exception ex)
        {
            progress.Complete(jobId, ex.Message);
            // Se algo falhar aqui, não apagamos a linha — o admin vê o erro e
            // repete, em vez de ficar com container/pasta órfãos e sem registo.
            return Result.Fail($"Não foi possível remover a instância: {ex.Message}");
        }

        await servers.RemoveAsync(id, ct); // 3. remove o registo

        progress.Complete(jobId);
        return Result.Success();
    }
}
