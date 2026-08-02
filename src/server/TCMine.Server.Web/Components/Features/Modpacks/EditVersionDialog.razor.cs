using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class EditVersionDialog : ComponentBase
{
    private bool _isSaving;
    private int? _memoryMb;

    private string _version = "";
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;
    [Parameter] public Guid VersionId { get; set; }
    [Parameter] public string Version { get; set; } = "";
    [Parameter] public int? MemoryMb { get; set; }

    protected override void OnInitialized()
    {
        _version = Version;
        _memoryMb = MemoryMb;
    }

    private async Task Save()
    {
        _isSaving = true;
        try
        {
            var result = await UpdateUseCase.HandleAsync(VersionId, _version, _memoryMb, CancellationToken.None);
            if (result.Succeeded) Dialog.Close(DialogResult.Ok(true));
            else Snackbar.Add(result.Error!, Severity.Error);
        }
        finally
        {
            _isSaving = false;
        }
    }
}
