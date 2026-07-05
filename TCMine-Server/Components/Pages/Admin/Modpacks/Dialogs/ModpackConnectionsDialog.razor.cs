using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine_Domain.Entities;
using TCMine_Server.Infrastructure.Minecraft;

namespace TCMine_Server.Components.Pages.Admin.Modpacks.Dialogs;

/// <summary>
///     Modal de conexões divulgadas manuais (Fase 3): carrega as entradas manuais do modpack, hospeda o
///     <c>ServersPanel</c> para edição e persiste-as ao confirmar (via <see cref="ModpackImportService" />).
///     As entradas auto-geradas por instâncias ficam fora — são geridas pela instância.
/// </summary>
public partial class ModpackConnectionsDialog : ComponentBase
{
    // Lista editável (cópias destacadas) — o ServersPanel edita por referência
    private List<ServerEntryEntity> _entries = [];
    [Inject] private ModpackImportService Service { get; set; } = null!;
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter] [EditorRequired] public Guid ModpackId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        _entries = await Service.GetManualConnectionsAsync(ModpackId);
    }

    private void Confirm()
    {
        // Devolve as entradas editadas; a persistência fica com o hub (padrão dos diálogos)
        MudDialog.Close(DialogResult.Ok(_entries));
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }
}