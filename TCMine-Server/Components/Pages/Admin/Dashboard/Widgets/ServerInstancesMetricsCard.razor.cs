using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine_Application.Contracts;
using TCMine_Domain.Entities;
using TCMine_Server.Infrastructure.ServerInstances;

namespace TCMine_Server.Components.Pages.Admin.Dashboard.Widgets;

/// <summary>
///     Grade de métricas por instância de servidor. Componente AUTOCONTIDO: gere o próprio
///     <see cref="Timer" /> e re-renderiza só a si mesmo, cruzando a lista de instâncias (nome/modpack/
///     status/RAM configurada, via <see cref="ServerInstanceService" />) com os stats ao vivo dos
///     containers (<see cref="ServerInstanceMetricsService" />). Isola o re-render periódico da página.
/// </summary>
public partial class ServerInstancesMetricsCard : ComponentBase, IDisposable
{
    // Intervalo de amostragem: mais folgado que o card de sistema (2s) porque cada stat de container é
    // uma leitura de ~1s no daemon; N servidores em paralelo a cada 5s mantém a carga sob controle.
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(5);

    // Uso em disco (bytes) por instância — vale para rodando E parada; cacheado no serviço
    private IReadOnlyDictionary<Guid, long> _disk = new Dictionary<Guid, long>();

    // Server List Ping por instância em execução → jogadores online (ServerPing.Online = contagem,
    // .Max = teto). Best-effort: só entra quem respondeu (offline = ausente do dicionário).
    private IReadOnlyDictionary<Guid, ServerPing> _pings = new Dictionary<Guid, ServerPing>();

    private List<ServerInstanceRowDto>? _rows;

    // Evita sobreposição de amostras: se uma rodada ainda não terminou, o próximo tick é ignorado
    private bool _sampling;

    private IReadOnlyDictionary<Guid, ServerInstanceStats> _stats =
        new Dictionary<Guid, ServerInstanceStats>();

    private Timer? _timer;

    [Inject] private ServerInstanceService Service { get; set; } = null!;
    [Inject] private ServerInstanceMetricsService Metrics { get; set; } = null!;

    public void Dispose()
    {
        _timer?.Dispose();
    }

    protected override async Task OnInitializedAsync()
    {
        // Primeira amostra imediata (no contexto do circuito → serviços scoped são seguros)
        await SampleAsync();

        _timer = new Timer(_ => _ = InvokeAsync(SampleAsync), null, SampleInterval, SampleInterval);
    }

    // Recarrega a lista de instâncias + os stats dos containers e re-renderiza. Sempre chamado no
    // contexto de sincronização do circuito (OnInitialized/InvokeAsync), então o AppDbContext scoped
    // do ServerInstanceService não é acessado concorrentemente.
    private async Task SampleAsync()
    {
        if (_sampling) return;
        _sampling = true;
        try
        {
            _rows = await Service.ListAsync();
            _stats = await Metrics.SampleAsync();
            // Uso em disco de todas as instâncias (não só as rodando) — cacheado, então é barato reamostrar
            _disk = await Metrics.SampleDiskAsync(_rows.Select(r => r.Id));
            // Jogadores online: ping (Server List Ping) só das instâncias em execução, em paralelo.
            // PingAsync não toca no AppDbContext (só na rede), então é seguro fora do lock scoped.
            _pings = await PingRunningAsync(_rows);
        }
        catch
        {
            // Falha transitória (daemon/BD) → mantém o último estado renderizado em vez de piscar erro
        }
        finally
        {
            _sampling = false;
        }

        StateHasChanged();
    }

    // Faz o Server List Ping das instâncias em execução, em paralelo, e devolve só as que responderam online.
    private async Task<IReadOnlyDictionary<Guid, ServerPing>> PingRunningAsync(IEnumerable<ServerInstanceRowDto> rows)
    {
        var running = rows.Where(r => r.Status == ServerInstanceStatus.Running).ToList();
        var result = new Dictionary<Guid, ServerPing>();
        if (running.Count == 0) return result;

        await Parallel.ForEachAsync(running, async (row, ct) =>
        {
            // Resposta não-nula = o servidor respondeu ao SLP (está online); null = sem resposta
            var ping = await Service.PingAsync(row.PublicAddress, row.Port, ct);
            if (ping is not null)
                lock (result)
                {
                    result[row.Id] = ping;
                }
        });

        return result;
    }

    // Stats do container desta instância, se estiver rodando e o daemon respondeu; senão null (card ocioso)
    private ServerInstanceStats? Stats(ServerInstanceRowDto row)
    {
        return _stats.TryGetValue(row.Id, out var s) ? s : null;
    }

    // Ping (jogadores) desta instância se respondeu online; senão null (esconde a linha de jogadores)
    private ServerPing? Ping(ServerInstanceRowDto row)
    {
        return _pings.TryGetValue(row.Id, out var p) ? p : null;
    }

    // Uso em disco desta instância, formatado (ou "—" enquanto a primeira medição não chegou)
    private string DiskLabel(ServerInstanceRowDto row)
    {
        return _disk.TryGetValue(row.Id, out var bytes) ? FormatSize(bytes) : "—";
    }

    // Tamanho em unidade adaptativa: GB a partir de 1 GB, senão MB
    private static string FormatSize(long bytes)
    {
        return bytes >= 1L << 30
            ? $"{bytes / 1024d / 1024d / 1024d:0.0} GB"
            : $"{bytes / 1024d / 1024d:0} MB";
    }

    // % de RAM usada sobre o -Xmx configurado (RamMb). O container não tem limite de cgroup, então o
    // denominador significativo é o heap máximo da JVM, não a memória total do host.
    private static double RamPercent(ServerInstanceStats s, ServerInstanceRowDto row)
    {
        if (row.RamMb <= 0) return 0;
        return Math.Clamp(s.MemoryUsedMb / row.RamMb * 100d, 0, 100);
    }

    // ── Apresentação do status (espelha a lista de servidores) ──────────────────────────────────────────

    private static string StatusLabel(ServerInstanceStatus s)
    {
        return s switch
        {
            ServerInstanceStatus.Running => "Em execução",
            ServerInstanceStatus.Starting => "Iniciando",
            ServerInstanceStatus.Stopping => "Parando",
            ServerInstanceStatus.Provisioning => "Provisionando",
            ServerInstanceStatus.Crashed => "Falhou",
            _ => "Parado"
        };
    }

    private static Color StatusColor(ServerInstanceStatus s)
    {
        return s switch
        {
            ServerInstanceStatus.Running => Color.Success,
            ServerInstanceStatus.Starting or ServerInstanceStatus.Stopping => Color.Info,
            ServerInstanceStatus.Provisioning => Color.Info,
            ServerInstanceStatus.Crashed => Color.Error,
            _ => Color.Default
        };
    }

    private static string StatusIcon(ServerInstanceStatus s)
    {
        return s switch
        {
            ServerInstanceStatus.Running => Icons.Material.Filled.CheckCircle,
            ServerInstanceStatus.Starting or ServerInstanceStatus.Stopping => Icons.Material.Filled.Sync,
            ServerInstanceStatus.Provisioning => Icons.Material.Filled.Sync,
            ServerInstanceStatus.Crashed => Icons.Material.Filled.Error,
            _ => Icons.Material.Filled.StopCircle
        };
    }
}