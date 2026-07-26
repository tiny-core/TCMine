using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Web.Components.Features.Modpacks;

namespace TCMine.Server.Web.Components.Pages.Modpacks;

public partial class ModpackDetailPage : ComponentBase
{
    private List<BreadcrumbItem> _breadcrumbs = [];

    private bool _isLoading = true;
    private Modpack? _modpack;
    private ModpackVersion? _selectedVersion;
    private Guid _selectedVersionId;

    [Parameter] public Guid ModpackId { get; set; }

    private int FileCount => _selectedVersion?.Files.Count ?? 0;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;

        _modpack = await Repository.GetWithVersionsAsync(ModpackId, CancellationToken.None);

        if (_modpack is not null)
        {
            _breadcrumbs =
            [
                new BreadcrumbItem("Modpacks", "/modpacks"),
                new BreadcrumbItem(_modpack.Name, null, true)
            ];

            // Seleciona a versão mais recente por padrão (a lista já vem
            // ordenada por Id decrescente na consulta).
            var first = _modpack.Versions
                .OrderByDescending(v => v.Id)
                .FirstOrDefault();

            if (first is not null)
            {
                _selectedVersionId = first.Id;
                _selectedVersion = first;
            }
        }

        _isLoading = false;
    }

    private void OnVersionChanged(Guid versionId)
    {
        _selectedVersionId = versionId;
        _selectedVersion = _modpack?.Versions.FirstOrDefault(v => v.Id == versionId);
    }

    private async Task OpenCreateVersion()
    {
        var parameters = new DialogParameters { ["ModpackId"] = ModpackId };
        var dialog = await DialogService.ShowAsync<CreateVersionDialog>("Nova versão", parameters);

        if (await dialog.Result is { Canceled: false })
            await LoadAsync();
    }

    private void OpenMods()
    {
        if (_selectedVersion is null)
            return;

        Navigation.NavigateTo($"/modpacks/{ModpackId}/versions/{_selectedVersion.Id}/mods");
    }

    private void OpenOverrides()
    {
        Navigation.NavigateTo($"/modpacks/{ModpackId}/versions/{_selectedVersionId}/overrides", true);
    }

    private async void OpenNews()
    {
        var parameters = new DialogParameters { ["ModpackId"] = ModpackId };
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true };
        await DialogService.ShowAsync<NewsDialog>("Novidades", parameters, options);
    }

    // Placeholders para os próximos passos.
    private void OpenServers()
    {
    }
}