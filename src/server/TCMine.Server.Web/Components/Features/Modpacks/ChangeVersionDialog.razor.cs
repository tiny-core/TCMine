using Microsoft.AspNetCore.Components;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Servers;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class ChangeVersionDialog
{
    private bool _isLoading = true;
    private Guid _selectedVersionId;
    private List<ModpackVersion> _versions = [];

    [Parameter] public GameServer Server { get; set; } = default!;

    [Inject] private IModpackRepository ModpackRepository { get; set; } = default!;
    [Inject] private ChangeServerVersion ChangeUseCase { get; set; } = default!;

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

    private Task Apply()
    {
        return SubmitAsync(
            () => ChangeUseCase.HandleAsync(Server.Id, _selectedVersionId, CancellationToken.None),
            "Versão do servidor alterada.");
    }
}
