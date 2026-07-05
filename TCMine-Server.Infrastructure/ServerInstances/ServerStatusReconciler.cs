using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TCMine_Server.Infrastructure.ServerInstances;

/// <summary>
///     Serviço de fundo que concilia periodicamente o status das instâncias com o daemon Docker, para o
///     painel refletir quedas/saídas de container mesmo quando ninguém está olhando. Cada ciclo cria o seu
///     próprio escopo de DI (o <see cref="DockerMinecraftManager" /> é scoped — usa o AppDbContext).
///     O <see cref="DockerMinecraftManager.ReconcileAllAsync" /> é barato quando não há instância ativa
///     (sai cedo, sem tocar no Docker), então o intervalo curto não pesa.
/// </summary>
public sealed class ServerStatusReconciler(IServiceScopeFactory scopes, ILogger<ServerStatusReconciler> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var manager = scope.ServiceProvider.GetRequiredService<DockerMinecraftManager>();
                await manager.ReconcileAllAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break; // shutdown
            }
            catch (Exception ex)
            {
                // Daemon offline / falha transitória — não derruba o serviço; tenta de novo no próximo ciclo
                logger.LogDebug(ex, "Reconciliação de status de servidor falhou (tentando de novo).");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}