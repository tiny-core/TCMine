using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine_Application.Contracts;

namespace TCMine_Server.Components.Pages.Admin.Modpacks.Dialogs;

/// <summary>
/// Diálogo que lista as atualizações de mods disponíveis (calculadas pelo editor) e deixa o admin
/// escolher quais aplicar. Devolve a lista selecionada de <see cref="ModUpdateDto"/>; aplicar é
/// responsabilidade do editor (troca FileId/versão/url no rascunho).
/// </summary>
public partial class ModUpdatesDialog : ComponentBase
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter] public List<ModUpdateDto> Updates { get; set; } = [];

    // Seleção da tabela (todos marcados por padrão)
    private HashSet<ModUpdateDto> _selected = [];

    protected override void OnInitialized()
    {
        _selected = [..Updates];
    }

    private void Confirm()
    {
        MudDialog.Close(DialogResult.Ok(_selected.ToList()));
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }
}
