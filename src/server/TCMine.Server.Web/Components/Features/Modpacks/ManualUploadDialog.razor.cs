using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Modpacks;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class ManualUploadDialog : ComponentBase
{
    private bool _isUploading;
    private FileSide _side = FileSide.Both;

    private string _targetFolder = "mods";

    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = null!;

    [Parameter] public Guid VersionId { get; set; }

    private async Task OnFileSelected(IBrowserFile file)
    {
        _isUploading = true;
        StateHasChanged();

        // Pasta escolhida + nome do arquivo. Assim um config.json vai para
        // config/ e um mod para mods/.
        var path = $"{_targetFolder}/{file.Name}";

        await using var stream = file.OpenReadStream(200 * 1024 * 1024);

        var command = new AddManualFileCommand(
            VersionId, path, stream, file.ContentType, _side, false);

        var result = await AddManualFileUseCase.HandleAsync(command, CancellationToken.None);

        _isUploading = false;

        if (result.Succeeded)
        {
            Snackbar.Add($"'{path}' adicionado.", Severity.Success);
            // Fecha devolvendo sucesso para a página recarregar.
            Dialog.Close(DialogResult.Ok(true));
        }
        else
        {
            Snackbar.Add(result.Error!, Severity.Error);
        }
    }
}