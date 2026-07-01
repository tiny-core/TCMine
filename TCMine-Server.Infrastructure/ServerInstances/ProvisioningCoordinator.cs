using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TCMine_Domain.Entities;
using TCMine_Server.Infrastructure.Persistence;

namespace TCMine_Server.Infrastructure.ServerInstances;

/// <summary>Estado de um job de provisionamento (para a UI reconectar).</summary>
public enum ProvisionState
{
    Running,
    Succeeded,
    Failed
}

/// <summary>Instantâneo imutável de um job de provisionamento — o que a página renderiza.</summary>
public sealed record ProvisionJobView(ProvisionState State, IReadOnlyList<string> Steps, string? Error);

/// <summary>
///     Coordena o provisionamento de instâncias de servidor <b>fora do circuito Blazor</b>: cada job roda
///     num escopo de DI próprio (via <see cref="IServiceScopeFactory" />) numa tarefa de fundo, guarda o
///     seu log de passos em memória e emite <see cref="Changed" /> a cada atualização. Assim:
///     <list type="bullet">
///         <item>um <b>refresh de página</b> não interrompe nem perde a provisão — a página só re-inscreve
///         e volta a ver o progresso;</item>
///         <item>a instância fica marcada como <see cref="ServerInstanceStatus.Provisioning" /> (persistido),
///         então um <b>reinício do TCMine-Server</b> no meio do processo é <b>retomado</b> no boot
///         (<see cref="RecoverAsync" />).</item>
///     </list>
///     Singleton: o estado dos jobs é partilhado por todos os circuitos.
/// </summary>
public sealed class ProvisioningCoordinator(IServiceScopeFactory scopeFactory, ILogger<ProvisioningCoordinator> logger)
{
    // Um job por instância. ConcurrentDictionary: acessado por circuitos e pela tarefa de fundo.
    private readonly ConcurrentDictionary<Guid, Job> _jobs = new();

    /// <summary>Disparado (com o id da instância) a cada mudança de estado de um job — a página re-renderiza.</summary>
    public event Action<Guid>? Changed;

    /// <summary>Instantâneo do job da instância, ou null se nunca houve/foi limpo.</summary>
    public ProvisionJobView? Get(Guid instanceId)
    {
        return _jobs.TryGetValue(instanceId, out var job) ? job.View() : null;
    }

    /// <summary>Há um provisionamento em andamento para esta instância?</summary>
    public bool IsRunning(Guid instanceId)
    {
        return _jobs.TryGetValue(instanceId, out var job) && job.State == ProvisionState.Running;
    }

    /// <summary>
    ///     Inicia (ou ignora, se já rodando) o provisionamento da instância. Retorna imediatamente — o
    ///     trabalho corre em segundo plano. <paramref name="applyUpdate" /> reinicia o servidor ao final se
    ///     ele estava rodando (equivalente ao "Aplicar atualização").
    /// </summary>
    public void Start(Guid instanceId, bool applyUpdate)
    {
        var job = new Job();

        // Não reinicia se já há um job em andamento; substitui um job terminado (nova tentativa)
        if (!_jobs.TryAdd(instanceId, job))
        {
            if (_jobs[instanceId].State == ProvisionState.Running) return;
            _jobs[instanceId] = job;
        }

        _ = RunAsync(instanceId, applyUpdate, job);
    }

    /// <summary>
    ///     Retoma no boot as provisões interrompidas: instâncias que ficaram em
    ///     <see cref="ServerInstanceStatus.Provisioning" /> (o server caiu no meio) são re-provisionadas do
    ///     zero (idempotente; o instalador remove um container órfão de mesmo nome antes de recriar).
    /// </summary>
    public async Task RecoverAsync(CancellationToken ct = default)
    {
        List<Guid> pending;
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            pending = await db.ServerInstances
                .Where(i => i.Status == ServerInstanceStatus.Provisioning)
                .Select(i => i.Id)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao ler instâncias pendentes de provisão no boot — pulando a retomada.");
            return;
        }

        foreach (var id in pending)
        {
            logger.LogInformation("Retomando provisionamento interrompido da instância {Id}.", id);
            Start(id, applyUpdate: false);
        }
    }

    private async Task RunAsync(Guid instanceId, bool applyUpdate, Job job)
    {
        // IProgress síncrono: o provisioner reporta sequencialmente; o Job coalesce/serializa sob lock
        var progress = new ActionProgress(message =>
        {
            job.Add(message);
            Changed?.Invoke(instanceId);
        });

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var service = scope.ServiceProvider.GetRequiredService<ServerInstanceService>();

            var instance = await db.ServerInstances.FirstOrDefaultAsync(i => i.Id == instanceId)
                           ?? throw new InvalidOperationException("Instância não encontrada.");

            // Captura o estado de execução ANTES de marcar Provisioning (senão o "aplicar update" perderia
            // a informação de que o servidor estava rodando e não o reiniciaria).
            var wasRunning = instance.Status == ServerInstanceStatus.Running;

            instance.Status = ServerInstanceStatus.Provisioning;
            instance.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            await service.ProvisionAsync(instanceId, progress);

            if (applyUpdate && wasRunning)
            {
                // Re-sobe com o loader/mods novos (StartAsync marca Running)
                await service.StartAsync(instanceId);
            }
            else
            {
                instance.Status = ServerInstanceStatus.Stopped;
                instance.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }

            job.Complete(null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao provisionar a instância {Id}.", instanceId);
            await ResetStatusAsync(instanceId); // tira de Provisioning para não re-tentar em loop no boot
            job.Complete(ex.Message);
        }

        Changed?.Invoke(instanceId);
    }

    // Best-effort: devolve o status a Stopped se ficou preso em Provisioning após uma falha
    private async Task ResetStatusAsync(Guid instanceId)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var instance = await db.ServerInstances.FirstOrDefaultAsync(i => i.Id == instanceId);
            if (instance is { Status: ServerInstanceStatus.Provisioning })
            {
                instance.Status = ServerInstanceStatus.Stopped;
                instance.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao restaurar o status da instância {Id} após erro de provisão.", instanceId);
        }
    }

    // ── Tipos internos ─────────────────────────────────────────────────────────────────────────────

    // IProgress que chama o handler de forma síncrona na thread do chamador (mantém a ordem dos passos)
    private sealed class ActionProgress(Action<string> report) : IProgress<string>
    {
        public void Report(string value)
        {
            report(value);
        }
    }

    // Estado mutável de um job — protegido por lock (mutação na tarefa de fundo, leitura no circuito)
    private sealed class Job
    {
        private const int MaxSteps = 60;
        private readonly List<string> _steps = [];
        private readonly object _lock = new();
        private string? _error;

        public ProvisionState State { get; private set; } = ProvisionState.Running;

        public void Add(string message)
        {
            lock (_lock)
            {
                // Coalesce atualizações ao vivo do mesmo passo (mesmo rótulo antes de " — ") — igual ao
                // BusyService, para o download/heartbeat não inflarem o log.
                static string Label(string s)
                {
                    var i = s.IndexOf(" — ", StringComparison.Ordinal);
                    return i < 0 ? s : s[..i];
                }

                if (_steps.Count > 0 && Label(_steps[^1]) == Label(message))
                    _steps[^1] = message;
                else
                    _steps.Add(message);

                if (_steps.Count > MaxSteps)
                    _steps.RemoveRange(0, _steps.Count - MaxSteps);
            }
        }

        public void Complete(string? error)
        {
            lock (_lock)
            {
                State = error is null ? ProvisionState.Succeeded : ProvisionState.Failed;
                _error = error;
            }
        }

        public ProvisionJobView View()
        {
            lock (_lock)
            {
                return new ProvisionJobView(State, [.. _steps], _error);
            }
        }
    }
}
