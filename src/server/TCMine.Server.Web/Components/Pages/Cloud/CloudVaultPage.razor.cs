using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Application.Cloud;
using TCMine.Server.Domain.Cloud;
using TCMine.Server.Web.Diagnostics;

namespace TCMine.Server.Web.Components.Pages.Cloud;

/// <summary>Uma nuvem: servidores ligados e chaves, jogadores e canais, configurações.</summary>
public partial class CloudVaultPage : ComponentBase
{
    /// <summary>Pendências por aba: o dono vê de longe onde há algo para decidir.</summary>
    private (int Suspects, int Quarantine, int Doubtful, int Incidents) _counts;

    private MudTabPanel _auditTab = default!;
    private string? _error;

    /// <summary>Jogador do último "Ver histórico": a aba Auditoria abre filtrada por ele.</summary>
    private string? _historyPlayer;

    /// <summary>Nome dos servidores do dono, para a aba de jogadores dizer quem segura cada lease.</summary>
    private IReadOnlyDictionary<Guid, string> _serverNames = new Dictionary<Guid, string>();

    private MudTabs _tabs = default!;
    private CloudVault? _vault;

    [Parameter] public Guid VaultId { get; set; }

    [Inject] private GetCloudVault GetUseCase { get; set; } = default!;
    [Inject] private ListCloudVaultServers ServersUseCase { get; set; } = default!;
    [Inject] private GetCloudOpenCounts CountsUseCase { get; set; } = default!;

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
        // Contados no banco: antes eram as quatro listas inteiras só para ler o
        // tamanho, e com as listas paginadas o tamanho já nem seria o total.
        var counts = await CountsUseCase.HandleAsync(VaultId, CancellationToken.None);
        _counts = counts.Value is { } c ? (c.Suspects, c.Quarantine, c.Doubtful, c.Incidents) : default;
    }

    // Pelo painel (@ref), e não pelo índice da aba: reordenar as abas não
    // pode fazer o botão abrir outra.
    private Task ShowHistory(string playerUuid)
    {
        _historyPlayer = playerUuid;
        return _tabs.ActivatePanelAsync(_auditTab);
    }

    private async Task ReloadServersAsync()
    {
        var servers = await ServersUseCase.HandleAsync(VaultId, CancellationToken.None);
        if (servers.Succeeded)
            _serverNames = servers.Value!.ToDictionary(s => s.ServerId, s => s.Name);
    }
}
