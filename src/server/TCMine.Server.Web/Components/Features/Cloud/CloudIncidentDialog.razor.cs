using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Application.Cloud;
using TCMine.Server.Domain.Cloud;
using TCMine.Server.Web.Components.Features.Modpacks;

namespace TCMine.Server.Web.Components.Features.Cloud;

/// <summary>Prévia do estorno de um rollback e a decisão (estornar ou aceitar).</summary>
public partial class CloudIncidentDialog : DialogComponentBase
{
    private List<CloudRevertLine>? _lines;

    [Parameter] [EditorRequired] public Guid VaultId { get; set; }
    [Parameter] [EditorRequired] public CloudRollbackIncident Incident { get; set; } = default!;
    [Parameter] public string ServerName { get; set; } = "";

    [Inject] private ResolveCloudIncident UseCase { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        var preview = await UseCase.PreviewAsync(VaultId, Incident.Id, CancellationToken.None);
        if (preview.Succeeded)
            _lines = [.. preview.Value!];
        else
            Snackbar.Add(preview.Error!, Severity.Error);
    }

    private Task RevertAsync() =>
        SubmitAsync(() => UseCase.HandleAsync(VaultId, Incident.Id, revert: true, CancellationToken.None), "Estorno feito.");

    private Task AcceptAsync() =>
        SubmitAsync(() => UseCase.HandleAsync(VaultId, Incident.Id, revert: false, CancellationToken.None), "Incidente aceito.");
}
