using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Cloud;
using TCMine.Server.Web.Components.Features.Cloud;

namespace TCMine.Server.Web.Components.Pages.Cloud;

/// <summary>As nuvens do usuário (todas, para o admin da instalação).</summary>
public partial class CloudVaultsPage : ComponentBase
{
    private bool _loaded;
    private List<CloudVaultSummary> _vaults = [];

    [Inject] private ListCloudVaults ListUseCase { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        _vaults = [.. await ListUseCase.HandleAsync(CancellationToken.None)];
        _loaded = true;
    }

    private async Task CreateAsync()
    {
        var dialog = await DialogService.ShowAsync<CreateCloudVaultDialog>("Nova nuvem");
        var result = await dialog.Result;
        if (result is { Canceled: false, Data: Guid id })
            Navigation.NavigateTo($"/admin/cloud/{id}");
    }

    private void Open(CloudVaultSummary vault) => Navigation.NavigateTo($"/admin/cloud/{vault.Id}");
}
