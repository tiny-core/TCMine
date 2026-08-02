using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Domain.Modpacks;
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
    private Guid _selectedVersionId;
    private List<ModpackVersion> _versions = [];

    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;

    [Parameter] public Guid ModpackId { get; set; }
    [Parameter] public GameServer? Existing { get; set; }

    private ModpackVersion? _selected => _versions.FirstOrDefault(v => v.Id == _selectedVersionId);
    private int SelectedModCount => _selected?.Files.Count(f => f.Origin != ModFileOrigin.Override) ?? 0;

    protected override async void OnInitialized()
    {
        _isNew = Existing is null;

        if (Existing is not null)
        {
            _name = Existing.Name;
            _connectAddress = Existing.ConnectAddress;
            _memoryMb = Existing.MemoryMb;
            _maxPlayers = Existing.MaxPlayers;
            return;
        }

        // Novo: só publicadas; a mais recente já vem selecionada.
        _versions =
        [
            .. (await ModpackRepository.ListVersionsAsync(ModpackId, CancellationToken.None))
            .Where(v => v.State is ModpackVersionState.Ready && !v.IsPreRelease)
        ];
        _selectedVersionId = _versions.FirstOrDefault()?.Id ?? Guid.Empty;
    }

    private async Task Save()
    {
        _isSaving = true;
        try
        {
            if (_isNew)
            {
                var result = await CreateUseCase.HandleAsync(
                    ModpackId, _name, _connectAddress, _memoryMb, _maxPlayers, _selectedVersionId,
                    CancellationToken.None);

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
