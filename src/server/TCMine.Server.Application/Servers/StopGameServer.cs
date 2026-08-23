using Microsoft.Extensions.Logging;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Security;

namespace TCMine.Server.Application.Servers;

public sealed partial class StopGameServer(
    IServerOrchestrator orchestrator,
    IServerRepository servers,
    IJobProgressReporter progress,
    ICurrentUserScope scope,
    ILogger<StopGameServer> logger)
{
    private readonly ILogger<StopGameServer> _logger = logger;

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao parar o servidor {ServerId}.")]
    private partial void LogFalha(Exception ex, Guid serverId);

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
            // Registrado além de devolvido: o Result vira um snackbar e
            // some com a página. Uma falha de infraestrutura — socket do
            // Docker sem permissão, imagem que não baixa — precisa deixar
            // rastro em algum lugar que sobreviva ao clique.
            LogFalha(ex, serverId);
            return Result.Fail($"Falha ao parar: {ex.Message}");
        }
    }
}
