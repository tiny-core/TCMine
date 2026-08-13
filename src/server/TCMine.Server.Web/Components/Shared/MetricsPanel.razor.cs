using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Web.Background;

namespace TCMine.Server.Web.Components.Shared;

public partial class MetricsPanel : ComponentBase, IDisposable
{
    private List<HostPoint> _host = [];
    private string[] _hostLabels = [];
    private List<ChartSeries<double>> _hostSeries = [];

    private Guid _selectedServerId;
    private string[] _serverLabels = [];
    private List<MetricPoint> _serverPoints = [];
    private List<ChartSeries<double>> _serverSeries = [];
    private List<GameServer> _servers = [];

    [Inject] private MetricsHistory History { get; set; } = default!;
    [Inject] private IServerRepository ServerRepository { get; set; } = default!;

    private double DiskUsedPercent
    {
        get
        {
            var last = _host[^1];
            return last.DiskTotalBytes is 0
                ? 0
                : (last.DiskTotalBytes - last.DiskFreeBytes) * 100d / last.DiskTotalBytes;
        }
    }

    // Disco cheio derruba servidor de jogo com o mundo aberto; o aviso tem de
    // vir antes de acabar, não depois.
    private Color DiskColor => DiskUsedPercent switch
    {
        >= 90 => Color.Error,
        >= 75 => Color.Warning,
        _ => Color.Success
    };

    private string LastHostLabel =>
        _host.Count is 0 ? "" : $"atualizado {_host[^1].At.ToLocalTime():HH:mm:ss}";

    public void Dispose()
    {
        History.Changed -= OnChanged;
        GC.SuppressFinalize(this);
    }

    protected override async Task OnInitializedAsync()
    {
        _servers = [.. await ServerRepository.ListAllAsync(CancellationToken.None)];
        _selectedServerId = _servers.FirstOrDefault()?.Id ?? Guid.Empty;

        History.Changed += OnChanged;
        Refresh();
    }

    private void OnChanged() => _ = InvokeAsync(() =>
    {
        Refresh();
        StateHasChanged();
    });

    private void Refresh()
    {
        _host = [.. History.Host()];
        _hostLabels = LabelsOf(_host.Select(p => p.At));

        _hostSeries =
        [
            new ChartSeries<double> { Name = "CPU %", Data = _host.Select(p => p.ProcessCpuPercent).ToArray() },
            new ChartSeries<double>
            {
                // Em MB para caber na mesma escala do percentual sem sumir.
                Name = "Memória (MB)",
                Data = _host.Select(p => p.ProcessMemoryBytes / 1024d / 1024d).ToArray()
            }
        ];

        _serverPoints = _selectedServerId == Guid.Empty ? [] : [.. History.Server(_selectedServerId)];
        _serverLabels = LabelsOf(_serverPoints.Select(p => p.At));

        _serverSeries =
        [
            new ChartSeries<double> { Name = "CPU %", Data = _serverPoints.Select(p => p.CpuPercent).ToArray() },
            new ChartSeries<double>
            {
                Name = "Memória (MB)",
                Data = _serverPoints.Select(p => p.MemoryUsedBytes / 1024d / 1024d).ToArray()
            }
        ];
    }

    private void OnServerChanged(Guid serverId)
    {
        _selectedServerId = serverId;
        Refresh();
    }

    /// <summary>
    ///     Rótulos do eixo X só a cada 8 pontos. Com 120 amostras, escrever a
    ///     hora em todas deixa o eixo ilegível.
    /// </summary>
    private static string[] LabelsOf(IEnumerable<DateTimeOffset> times) =>
    [
        .. times.Select((t, i) => i % 8 == 0 ? t.ToLocalTime().ToString("HH:mm") : "")
    ];
}
