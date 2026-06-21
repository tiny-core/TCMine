using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine_Infrastructure.Server;

namespace TCMine_Server.Components.Pages.Admin.Widgets;

/// <summary>
/// Card de status do sistema. Encapsula a amostragem periódica das métricas do processo
/// (memória/heap/threads/uptime) e o histórico do gráfico. Por gerir o próprio
/// <see cref="Timer"/>, isola o re-render de 2 em 2 segundos — a página do dashboard não
/// re-renderiza (as outras seções têm dados imutáveis carregados uma única vez).
/// </summary>
public partial class SystemStatusCard : ComponentBase, IDisposable
{
    [Inject] private SystemMetricsService Metrics { get; set; } = null!;

    // Janela deslizante das últimas amostras de memória (MB)
    private const int MaxPoints = 30;
    private readonly List<double> _memHistory = [];

    private List<ChartSeries<double>> _series = [];
    private string[] _labels = [];

    // Série única → legenda redundante (o rótulo já está no cabeçalho do card)
    private readonly ChartOptions _chartOptions = new() { ShowLegend = false };

    private SystemSnapshot _snapshot;
    private Timer? _timer;

    private string Uptime => FormatUptime(_snapshot.Uptime);

    // Pico de memória da janela atual — contextualiza o valor instantâneo
    private double PeakMemoryMb => _memHistory.Count == 0 ? 0 : _memHistory.Max();

    protected override void OnInitialized()
    {
        // Primeira amostra imediata para o gráfico não nascer vazio
        Sample();

        // A cada 2s: nova amostra + re-render só deste componente. InvokeAsync porque o
        // callback do Timer roda fora do contexto de sincronização do renderer Blazor.
        _timer = new Timer(_ =>
        {
            Sample();
            _ = InvokeAsync(StateHasChanged);
        }, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    private void Sample()
    {
        _snapshot = Metrics.Capture();

        _memHistory.Add(Math.Round(_snapshot.MemoryMb, 1));
        if (_memHistory.Count > MaxPoints)
            _memHistory.RemoveAt(0);

        // Recria a série a cada amostra para o MudChart detectar a mudança
        _series = [new ChartSeries<double> { Name = "Memória (MB)", Data = _memHistory.ToArray() }];
        // Eixo X sem rótulos — é uma visão "últimas N amostras", não um relógio
        _labels = _memHistory.Select(_ => string.Empty).ToArray();
    }

    private static string FormatUptime(TimeSpan t)
    {
        return t.TotalDays >= 1
            ? $"{(int)t.TotalDays}d {t.Hours}h {t.Minutes}m"
            : t.TotalHours >= 1
                ? $"{(int)t.TotalHours}h {t.Minutes}m"
                : $"{t.Minutes}m {t.Seconds}s";
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}