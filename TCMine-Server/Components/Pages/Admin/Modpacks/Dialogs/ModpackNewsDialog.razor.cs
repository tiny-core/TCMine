using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace TCMine_Server.Components.Pages.Admin.Modpacks.Dialogs;

/// <summary>
/// Modal de novidades do modpack (Fase 3): hospeda o <c>NewsPanel</c> (self-contained, grava sozinho).
/// Sem persistência aqui — só fecha.
/// </summary>
public partial class ModpackNewsDialog : ComponentBase
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter] [EditorRequired] public Guid ModpackId { get; set; }

    private void Close()
    {
        MudDialog.Close();
    }
}
