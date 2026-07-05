using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine_Application.Contracts;

namespace TCMine_Server.Components.Pages.Admin.Modpacks.Dialogs;

/// <summary>
///     Seletor de versão de um mod do CurseForge: lista os arquivos (buscados lazy pelo editor) e devolve o
///     escolhido ao clicar. A aplicação no rascunho (trocar FileId/versão e forçar re-download) fica com o editor.
/// </summary>
public partial class ModVersionPickerDialog : ComponentBase
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>Versões disponíveis (já buscadas, filtradas por MC+loader quando possível).</summary>
    [Parameter]
    [EditorRequired]
    public List<CfFileRefDto> Files { get; set; } = [];

    /// <summary>FileId atual do mod — marcado como "atual" na lista.</summary>
    [Parameter]
    public long CurrentFileId { get; set; }

    private void Pick(CfFileRefDto file)
    {
        MudDialog.Close(DialogResult.Ok(file));
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }
}