using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine_Domain.Entities;

namespace TCMine_Server.Components.Pages.Admin.Modpacks.Dialogs;

/// <summary>
///     Modal de metadados do modpack: hospeda o <c>DetailsPanel</c> sobre um rascunho (já clonado pelo hub,
///     para o cancelar não vazar edições) e devolve-o ao confirmar. A persistência fica com o hub
///     (<c>UpdateMetadataAsync</c>), seguindo o padrão dos outros diálogos (coletam, a página grava).
/// </summary>
public partial class ModpackDetailsDialog : ComponentBase
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>Rascunho editável (clone do modpack) — o DetailsPanel edita por referência.</summary>
    [Parameter]
    public ModpackEntity Draft { get; set; } = null!;

    private void Confirm()
    {
        MudDialog.Close(DialogResult.Ok(Draft));
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }
}