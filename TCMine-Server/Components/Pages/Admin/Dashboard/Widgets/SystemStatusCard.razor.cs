using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine_Server.Infrastructure.Server;

namespace TCMine_Server.Components.Pages.Admin.Dashboard.Widgets;

/// <summary>
///     Card de status do sistema. Encapsula a amostragem periódica das métricas do processo
///     (memória/heap/threads/uptime) e o histórico do gráfico. Por gerir o próprio
///     <see cref="Timer" />, isola o re-render de 2 em 2 segundos — a página do dashboard não
///     re-renderiza (as outras seções têm dados imutáveis carregados uma única vez).
/// </summary>
public partial class SystemStatusCard : ComponentBase, IDisposable
{
    // Janela deslizante das últimas amostras de memória (MB)
    private const int MaxPoints = 30;

    // Gráfico de memória: série única → sem legenda (o rótulo está no cabeçalho da seção)
    private readonly ChartOptions _memChartOptions = new() { ShowLegend = false };
    private readonly List<double> _memHistory = [];

    // Gráfico de rede: séries ↓ recebido e ↑ enviado (mesma escala MB/s) → legenda ligada
    private readonly ChartOptions _netChartOptions = new() { ShowLegend = true };
    private readonly List<double> _netRecvHistory = [];
    private readonly List<double> _netSentHistory = [];
    private string[] _labels = [];

    private List<ChartSeries<double>> _memSeries = [];
    private List<ChartSeries<double>> _netSeries = [];

    private SystemSnapshot _snapshot;
    private Timer? _timer;
    [Inject] private SystemMetricsService Metrics { get; set; } = null!;

    private string Uptime => FormatUptime(_snapshot.Uptime);

    // Pico de memória da janela atual — contextualiza o valor instantâneo
    private double PeakMemoryMb => _memHistory.Count == 0 ? 0 : _memHistory.Max();

    public void Dispose()
    {
        _timer?.Dispose();
    }

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

        _memHistory.Add(Math.Round(_snapshot.WorkingSetMb, 1));
        if (_memHistory.Count > MaxPoints)
            _memHistory.RemoveAt(0);

        // 3 casas: a taxa costuma ser pequena (MB/s) e não queremos perder amostras baixas
        _netRecvHistory.Add(Math.Round(_snapshot.NetRecvMbps, 3));
        _netSentHistory.Add(Math.Round(_snapshot.NetSentMbps, 3));
        if (_netRecvHistory.Count > MaxPoints)
        {
            _netRecvHistory.RemoveAt(0);
            _netSentHistory.RemoveAt(0);
        }

        // Recria as séries a cada amostra para o MudChart detectar a mudança
        _memSeries = [new ChartSeries<double> { Name = "Memória (MB)", Data = _memHistory.ToArray() }];
        _netSeries =
        [
            new ChartSeries<double> { Name = "↓ Recebido", Data = _netRecvHistory.ToArray() },
            new ChartSeries<double> { Name = "↑ Enviado", Data = _netSentHistory.ToArray() }
        ];
        // Eixo X sem rótulos — é uma visão "últimas N amostras", não um relógio
        _labels = _memHistory.Select(_ => string.Empty).ToArray();
    }

    // Formata uma taxa em bytes/s na maior unidade legível (B/s, KB/s, MB/s)
    private static string FormatRate(double bytesPerSec)
    {
        return bytesPerSec >= 1024 * 1024
            ? $"{bytesPerSec / 1024d / 1024d:0.0} MB/s"
            : bytesPerSec >= 1024
                ? $"{bytesPerSec / 1024d:0} KB/s"
                : $"{bytesPerSec:0} B/s";
    }

    private static string FormatUptime(TimeSpan t)
    {
        return t.TotalDays >= 1
            ? $"{(int)t.TotalDays}d {t.Hours}h {t.Minutes}m"
            : t.TotalHours >= 1
                ? $"{(int)t.TotalHours}h {t.Minutes}m"
                : $"{t.Minutes}m {t.Seconds}s";
    }
}