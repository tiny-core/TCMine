using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace TCMine_Server.Components.Pages.Admin.Releases.Dialogs;

/// <summary>
///     Diálogo de confirmação da compilação do launcher. A versão é fixa (a do servidor, passada pela
///     página) — só coleta as notas e devolve um <see cref="LauncherBuildRequest" />.
/// </summary>
public partial class LauncherBuildDialog : ComponentBase
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;

    // Versão-alvo (= última launcher-v*); mostrada só para leitura
    [Parameter] public string Version { get; set; } = "0.0.0";

    // Notas pré-preenchidas (ex.: corpo da release do GitHub); o admin pode ajustar
    [Parameter] public string InitialNotes { get; set; } = string.Empty;

    private string _notes = string.Empty;

    protected override void OnInitialized()
    {
        _notes = InitialNotes;
    }

    private void Confirm()
    {
        MudDialog.Close(DialogResult.Ok(new LauncherBuildRequest(Version, _notes.Trim())));
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }

    /// <summary>Pedido de build entregue à página.</summary>
    public sealed record LauncherBuildRequest(string Version, string Notes);
}
