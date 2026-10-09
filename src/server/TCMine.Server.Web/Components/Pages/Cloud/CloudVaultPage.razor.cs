using Microsoft.AspNetCore.Components;
using TCMine.Server.Application.Cloud;
using TCMine.Server.Domain.Cloud;
using TCMine.Server.Web.Diagnostics;

namespace TCMine.Server.Web.Components.Pages.Cloud;

/// <summary>Uma nuvem: servidores ligados e chaves, jogadores e canais, configurações.</summary>
public partial class CloudVaultPage : ComponentBase
{
    /// <summary>Pendências por aba: o dono vê de longe onde há algo para decidir.</summary>
    private (int Suspects, int Quarantine, int Doubtful, int Incidents) _counts;

    private string? _error;

    /// <summary>Nome dos servidores do dono, para a aba de jogadores dizer quem segura cada lease.</summary>
    private IReadOnlyDictionary<Guid, string> _serverNames = new Dictionary<Guid, string>();

    private CloudVault? _vault;

    [Parameter] public Guid VaultId { get; set; }

    [Inject] private GetCloudVault GetUseCase { get; set; } = default!;
    [Inject] private ListCloudVaultServers ServersUseCase { get; set; } = default!;
    [Inject] private ListCloudSuspects SuspectsUseCase { get; set; } = default!;
    [Inject] private ListCloudQuarantine QuarantineUseCase { get; set; } = default!;
    [Inject] private ListCloudDoubtful DoubtfulUseCase { get; set; } = default!;
    [Inject] private ListCloudIncidents IncidentsUseCase { get; set; } = default!;

    // TEMPORÁRIO (linha de base, sai no fim da fase 8).
    [Inject] private PageLoadTimer LoadTimer { get; set; } = default!;

    /// <summary>MudBlazor esconde o badge quando o dado é nulo.</summary>
    private static int? Badge(int count) => count > 0 ? count : null;

    protected override async Task OnParametersSetAsync()
    {
        LoadTimer.Start(nameof(CloudVaultPage));
        await LoadAsync();
        LoadTimer.Loaded();
    }

    protected override void OnAfterRender(bool firstRender) => LoadTimer.Rendered();

    private async Task LoadAsync()
    {
        var result = await GetUseCase.HandleAsync(VaultId, CancellationToken.None);
        if (!result.Succeeded)
        {
            _error = result.Error;
            return;
        }

        _vault = result.Value;
        await ReloadServersAsync();
        await LoadCountsAsync();
    }

    private async Task LoadCountsAsync()
    {
        var ct = CancellationToken.None;
        var suspects = await SuspectsUseCase.HandleAsync(VaultId, true, ct);
        var quarantine = await QuarantineUseCase.HandleAsync(VaultId, true, ct);
        var doubtful = await DoubtfulUseCase.HandleAsync(VaultId, true, ct);
        var incidents = await IncidentsUseCase.HandleAsync(VaultId, true, ct);
        _counts = (suspects.Value?.Count ?? 0, quarantine.Value?.Count ?? 0, doubtful.Value?.Count ?? 0,
            incidents.Value?.Count ?? 0);
    }

    private async Task ReloadServersAsync()
    {
        var servers = await ServersUseCase.HandleAsync(VaultId, CancellationToken.None);
        if (servers.Succeeded)
            _serverNames = servers.Value!.ToDictionary(s => s.ServerId, s => s.Name);
    }
}
