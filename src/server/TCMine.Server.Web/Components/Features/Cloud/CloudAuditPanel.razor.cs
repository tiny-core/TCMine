using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Cloud;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Web.Components.Features.Cloud;

/// <summary>Aba "Auditoria": quem decidiu o quê no painel e o histórico de saldos (o ledger).</summary>
public partial class CloudAuditPanel : ComponentBase
{
    private List<CloudAdminAuditEntry>? _actions;
    private List<CloudLedgerView>? _ledger;
    private string? _player;

    [Parameter] [EditorRequired] public Guid VaultId { get; set; }

    [Inject] private ListCloudAudit UseCase { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        var result = await UseCase.HandleAsync(VaultId, _player, CancellationToken.None);
        if (!result.Succeeded)
        {
            Snackbar.Add(result.Error!, Severity.Error);
            return;
        }

        _actions = [.. result.Value.Actions];
        _ledger = [.. result.Value.Ledger];
    }

    private static string SourceLabel(CloudLedgerSource source) => source switch
    {
        CloudLedgerSource.Game => "Jogo",
        CloudLedgerSource.Admin => "Painel",
        CloudLedgerSource.Revert => "Estorno",
        _ => "Quarentena"
    };
}
