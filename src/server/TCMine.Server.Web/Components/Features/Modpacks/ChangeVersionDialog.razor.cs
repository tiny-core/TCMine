using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class ChangeVersionDialog : ComponentBase
{
    private bool _isLoading = true;
    private bool _isSaving;
    private Guid _selectedVersionId;
    private List<ModpackVersion> _versions = [];
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;

    [Parameter] public GameServer Server { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        if (Server.HasWorld)
        {
            _isLoading = false;
            return;
        }

        var all = await ModpackRepository.ListVersionsAsync(Server.ModpackId, CancellationToken.None);
        // Só instaláveis: publicadas ou arquivadas (arquivada continua rodável
        // por quem já a fixou — é o alvo natural de um rollback).
        _versions =
        [
            .. all
                .Where(v => v.State is ModpackVersionState.Ready or ModpackVersionState.Archived)
        ];
        _selectedVersionId = Server.ModpackVersionId;
        _isLoading = false;
    }

    private async Task Apply()
    {
        _isSaving = true;
        try
        {
            var result = await ChangeUseCase.HandleAsync(Server.Id, _selectedVersionId, CancellationToken.None);
            if (result.Succeeded)
            {
                Snackbar.Add("Versão do servidor alterada.", Severity.Success);
                Dialog.Close(DialogResult.Ok(true));
            }
            else
                Snackbar.Add(result.Error!, Severity.Error);
        }
        finally
        {
            _isSaving = false;
        }
    }
}
