using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Application.Cloud;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Web.Components.Features.Cloud;

/// <summary>Aba "Incidentes": mundos que voltaram no tempo, com a prévia do estorno.</summary>
public partial class CloudIncidentsPanel : ComponentBase
{
    private List<CloudRollbackIncident>? _items;
    private bool _showResolved;

    [Parameter] [EditorRequired] public Guid VaultId { get; set; }
    [Parameter] public IReadOnlyDictionary<Guid, string> ServerNames { get; set; } = new Dictionary<Guid, string>();
    [Parameter] public EventCallback Changed { get; set; }

    [Inject] private ListCloudIncidents ListUseCase { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        var result = await ListUseCase.HandleAsync(VaultId, openOnly: !_showResolved, CancellationToken.None);
        _items = result.Succeeded ? [.. result.Value!] : [];
    }

    private async Task OpenAsync(CloudRollbackIncident incident)
    {
        var parameters = new DialogParameters<CloudIncidentDialog>
        {
            { x => x.VaultId, VaultId },
            { x => x.Incident, incident },
            { x => x.ServerName, ServerNames.GetValueOrDefault(incident.ServerId, "servidor removido") }
        };
        var dialog = await DialogService.ShowAsync<CloudIncidentDialog>("Incidente", parameters,
            new DialogOptions { MaxWidth = MaxWidth.Large, FullWidth = true });
        if (await dialog.Result is { Canceled: false })
        {
            await LoadAsync();
            await Changed.InvokeAsync();
        }
    }
}
