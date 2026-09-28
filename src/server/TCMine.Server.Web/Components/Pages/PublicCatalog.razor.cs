using Microsoft.AspNetCore.Components;
using TCMine.Contracts.Modpacks;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Public;

namespace TCMine.Server.Web.Components.Pages;

public partial class PublicCatalog : ComponentBase, IAsyncDisposable
{
    /// <summary>
    ///     Mesmo intervalo do MetricsCollector: reamostrar mais rápido que a
    ///     própria coleta só mostraria o mesmo número de novo.
    /// </summary>
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(15);

    private PublicCatalogView _catalog = new([], []);
    private LauncherReleaseInfo? _launcher;
    private bool _isLoading = true;
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;

    [Inject] private GetPublicCatalog CatalogUseCase { get; set; } = default!;
    [Inject] private ILauncherReleaseSource LauncherSource { get; set; } = default!;

    private int TotalOnline => _catalog.Servers.Sum(s => s.OnlinePlayers ?? 0);
    private int OnlineServerCount => _catalog.Servers.Count(s => s.Status is GameServerStatus.Running);

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
        _isLoading = false;

        _cts = new CancellationTokenSource();
        _ = RefreshLoopAsync(_cts.Token);
    }

    private async Task LoadAsync()
    {
        _catalog = await CatalogUseCase.HandleAsync(CancellationToken.None);
        _launcher = await LauncherSource.GetLatestAsync(CancellationToken.None);
    }

    private async Task RefreshLoopAsync(CancellationToken ct)
    {
        _timer = new PeriodicTimer(RefreshInterval);

        try
        {
            while (await _timer.WaitForNextTickAsync(ct))
            {
                await LoadAsync();
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException)
        {
            // Saída normal: o visitante fechou a aba.
        }
    }

    private static string LoaderLabel(ModLoader loader) => loader switch
    {
        ModLoader.Vanilla => "Vanilla",
        ModLoader.Forge => "Forge",
        ModLoader.NeoForge => "NeoForge",
        ModLoader.Fabric => "Fabric",
        ModLoader.Quilt => "Quilt",
        _ => loader.ToString()
    };

    private static string StatusLabel(GameServerStatus status) => status switch
    {
        GameServerStatus.Running => "Online",
        GameServerStatus.Starting => "Iniciando",
        GameServerStatus.Stopping => "Parando",
        GameServerStatus.Updating => "Atualizando",
        GameServerStatus.Crashed => "Falhou",
        _ => "Offline"
    };

    public async ValueTask DisposeAsync()
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
        }

        _timer?.Dispose();
        GC.SuppressFinalize(this);
    }
}
