using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class NewOverrideDialog : ComponentBase
{
    private const string NewFolderSentinel = "\u0000new";
    private string _customFolder = "";
    private string _fileName = "";

    private string _folder = "";
    private bool _isSaving;
    private IBrowserFile? _upload;
    private string? _uploadName;
    private string? NameHelper => _uploadName is not null ? "Usando o nome do arquivo enviado." : null;

    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;
    [Parameter] public Guid VersionId { get; set; }
    [Parameter] public IReadOnlyList<string> Folders { get; set; } = [];

    private string EffectiveFolder =>
        _folder == NewFolderSentinel ? _customFolder.Trim().Trim('/') : _folder;

    private string EffectiveName => _uploadName ?? _fileName.Trim();

    private bool CanConfirm =>
        !string.IsNullOrWhiteSpace(EffectiveName)
        && (_folder != NewFolderSentinel || !string.IsNullOrWhiteSpace(_customFolder));

    private void OnFilePicked(IBrowserFile? file)
    {
        _upload = file;
        _uploadName = file?.Name;
    }

    private void ClearUpload()
    {
        _upload = null;
        _uploadName = null;
    }

    private async Task Confirm()
    {
        _isSaving = true;
        try
        {
            var path = string.IsNullOrEmpty(EffectiveFolder)
                ? EffectiveName
                : $"{EffectiveFolder}/{EffectiveName}";

            Result result;
            if (_upload is not null)
            {
                // Limite defensivo (10 MB) — overrides não são mundos.
                await using var stream = _upload.OpenReadStream(10 * 1024 * 1024);
                result = await SaveUseCase.HandleAsync(
                    VersionId, path, stream, _upload.ContentType, CancellationToken.None);
            }
            else
            {
                result = await SaveUseCase.HandleAsync(VersionId, path, "", CancellationToken.None);
            }

            if (result.Succeeded)
                Dialog.Close(DialogResult.Ok(path)); // devolve o path criado
            else
                Snackbar.Add(result.Error!, Severity.Error);
        }
        finally
        {
            _isSaving = false;
        }
    }
}