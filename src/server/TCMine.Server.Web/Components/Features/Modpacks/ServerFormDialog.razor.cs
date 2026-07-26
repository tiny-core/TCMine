using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class ServerFormDialog : ComponentBase
{
    private string _connectAddress = "";

    private bool _isNew;
    private bool _isSaving;
    private int _maxPlayers = 20;
    private int _memoryMb = 4096;
    private string _name = "";
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;

    [Parameter] public Guid ModpackId { get; set; }
    [Parameter] public GameServer? Existing { get; set; }

    protected override void OnInitialized()
    {
        _isNew = Existing is null;
        if (Existing is not null)
        {
            _name = Existing.Name;
            _connectAddress = Existing.ConnectAddress;
            _memoryMb = Existing.MemoryMb;
            _maxPlayers = Existing.MaxPlayers;
        }
    }

    private async Task Save()
    {
        _isSaving = true;
        try
        {
            if (_isNew)
            {
                var result = await CreateUseCase.HandleAsync(
                    ModpackId, _name, _connectAddress, _memoryMb, _maxPlayers, CancellationToken.None);
                if (!result.Succeeded)
                {
                    Snackbar.Add(result.Error!, Severity.Error);
                    return;
                }
            }
            else
            {
                var result = await UpdateUseCase.HandleAsync(
                    Existing!.Id, _name, _connectAddress, _memoryMb, _maxPlayers, CancellationToken.None);
                if (!result.Succeeded)
                {
                    Snackbar.Add(result.Error!, Severity.Error);
                    return;
                }
            }

            Snackbar.Add("Servidor salvo.", Severity.Success);
            Dialog.Close(DialogResult.Ok(true));
        }
        finally
        {
            _isSaving = false;
        }
    }
}