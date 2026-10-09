using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Cloud;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Security;

namespace TCMine.Server.Application.Servers;

public sealed partial class StartGameServer(
    IServerOrchestrator orchestrator,
    IServerRepository servers,
    IJobProgressReporter progress,
    ICurrentUserScope scope,
    IServerWhitelistSync whitelist,
    ProvisionServerCloudKey cloudKey,
    ILogger<StartGameServer> logger)
{
    private readonly ILogger<StartGameServer> _logger = logger;

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao iniciar o servidor {ServerId}.")]
    private partial void LogFalha(Exception ex, Guid serverId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Nuvem: não consegui preparar a chave do servidor {ServerId}; ele sobe sem nuvem.")]
    private partial void LogFalhaNuvem(Exception ex, Guid serverId);

    // TEMPORÁRIO — linha de base da refatoração (docs/BASELINE.md, S5). Sai no
    // fim da fase 8. "container" é o orchestrator.StartAsync: materializar a
    // pasta, criar e iniciar o container. NÃO inclui o Minecraft carregar os
    // mods — Running aqui é "container no ar", e não "aceitando jogadores".
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Início de servidor: {ServerId} — total {TotalMs} ms (container {ContainerMs} ms).")]
    private partial void LogStartTimings(Guid serverId, long totalMs, long containerMs);

    public async Task<Result> HandleAsync(Guid serverId, CancellationToken ct, Guid jobId = default)
    {
        var auth = await scope.RequireAsync(serverId, ServerAccessPolicy.CanControlPower, ct);
        if (!auth.Succeeded)
            return auth;

        var server = await servers.GetByIdAsync(serverId, ct);
        if (server is null)
            return Result.Fail("Servidor não encontrado.");

        var startedAt = Stopwatch.GetTimestamp();

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
            // Antes do start: o mod lê o arquivo da chave no arranque. Falhar aqui
            // não pode impedir o jogo de subir — sem arquivo, só a nuvem fica
            // desligada nesse servidor.
            try
            {
                await cloudKey.HandleAsync(serverId, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFalhaNuvem(ex, serverId);
            }

            Report("Preparando a instância e o container…");
            // EnsureCreated (dentro do Start) materializa a pasta, cria o
            // container e persiste o ContainerId. É idempotente.
            var containerStartedAt = Stopwatch.GetTimestamp();
            await orchestrator.StartAsync(serverId, ct);
            var containerMs = (long)Stopwatch.GetElapsedTime(containerStartedAt).TotalMilliseconds;

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

            // Depois de o servidor estar de pé: é quando o RCON responde. Um
            // servidor recriado começa com a whitelist vazia, e sem isto os
            // membros levariam "not white-listed" numa lista que o painel jura
            // que eles integram.
            await whitelist.HandleAsync(serverId, ct);

            progress.Complete(jobId);

            var totalMs = (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            LogStartTimings(serverId, totalMs, containerMs);

            return Result.Success();
        }
        catch (Exception ex)
        {
            // Docker fora do ar, imagem a puxar, porta ocupada… o admin precisa
            // de ver a causa, não um erro genérico.
            progress.Complete(jobId, ex.Message);
            // Registrado além de devolvido: o Result vira um snackbar e
            // some com a página. Uma falha de infraestrutura — socket do
            // Docker sem permissão, imagem que não baixa — precisa deixar
            // rastro em algum lugar que sobreviva ao clique.
            LogFalha(ex, serverId);
            return Result.Fail($"Falha ao iniciar: {ex.Message}");
        }
    }
}
