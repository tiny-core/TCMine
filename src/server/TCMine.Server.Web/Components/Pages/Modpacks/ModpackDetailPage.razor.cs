using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Web.Components.Features.Modpacks;

namespace TCMine.Server.Web.Components.Pages.Modpacks;

public partial class ModpackDetailPage : ComponentBase
{
    private List<BreadcrumbItem> _breadcrumbs = [];

    private bool _isLoading = true;

    private bool _isPublishing;
    private Modpack? _modpack;
    private ModpackVersion? _selectedVersion;
    private Guid _selectedVersionId;

    [Parameter] public Guid ModpackId { get; set; }

    private int FileCount => _selectedVersion?.Files.Count ?? 0;

    [Inject] private PublishModpackVersion PublishUseCase { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    [Inject] private DeleteModpackVersion DeleteVersionUseCase { get; set; } = default!;

    protected override async Task OnInitializedAsync() => await LoadAsync();

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
            // ordenada por ID decrescente na consulta).
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
        var latest = _modpack?.Versions
            .OrderByDescending(v => v.Id)
            .FirstOrDefault();

        var parameters = new DialogParameters
        {
            ["ModpackId"] = ModpackId,
            ["DefaultVersion"] = latest?.Version,
            ["MinecraftVersion"] = _modpack?.MinecraftVersion,
            ["Loader"] = _modpack?.Loader,
            ["DefaultLoaderVersion"] = latest?.LoaderVersion,
            ["DefaultMemoryMb"] = latest?.RecommendedMemoryMb
        };

        var dialog = await DialogService.ShowAsync<CreateVersionDialog>("Nova versão", parameters);

        if (await dialog.Result is { Canceled: false })
            await LoadAsync();
    }

    private async Task DeleteVersion()
    {
        if (_selectedVersion is null)
            return;

        var confirm = await DialogService.ShowMessageBoxAsync(
            "Apagar rascunho",
            $"Apagar a versão {_selectedVersion.Version}? Os mods e overrides desta versão são removidos. Irreversível.",
            "Apagar", cancelText: "Cancelar");
        if (confirm is not true)
            return;

        var result = await DeleteVersionUseCase.HandleAsync(_selectedVersion.Id, CancellationToken.None);
        if (result.Succeeded)
        {
            Snackbar.Add("Rascunho apagado.", Severity.Success);
            _selectedVersionId = Guid.Empty; // a seleção atual já não existe
            await LoadAsync();
        }
        else
            Snackbar.Add(result.Error!, Severity.Error);
    }

    private async Task Publish()
    {
        if (_selectedVersion is null)
            return;

        var confirm = await DialogService.ShowMessageBoxAsync(
            "Publicar versão",
            $"Publicar a versão {_selectedVersion.Version}? A partir daqui ela fica imutável — "
            + "para mudanças, cria uma nova versão.",
            "Publicar", cancelText: "Cancelar");
        if (confirm is not true)
            return;

        _isPublishing = true;
        try
        {
            var result = await PublishUseCase.HandleAsync(_selectedVersion.Id, CancellationToken.None);
            if (result.Succeeded)
            {
                Snackbar.Add("Versão publicada.", Severity.Success);
                await LoadAsync(); // recarrega: o chip vira Publicado, o Publicar some
            }
            else
                Snackbar.Add(result.Error!, Severity.Error);
        }
        finally
        {
            _isPublishing = false;
        }
    }

    private async Task OpenEditVersion()
    {
        var p = new DialogParameters
        {
            ["VersionId"] = _selectedVersion!.Id,
            ["Version"] = _selectedVersion.Version,
            ["MemoryMb"] = _selectedVersion.RecommendedMemoryMb
        };
        var dialog = await DialogService.ShowAsync<EditVersionDialog>("Editar versão", p);
        if (await dialog.Result is { Canceled: false }) await LoadAsync();
    }

    private void OpenMods()
    {
        if (_selectedVersion is null)
            return;

        Navigation.NavigateTo($"/modpacks/{ModpackId}/versions/{_selectedVersion.Id}/mods");
    }

    private void OpenOverrides() =>
        Navigation.NavigateTo($"/modpacks/{ModpackId}/versions/{_selectedVersionId}/overrides", true);

    private async void OpenNews()
    {
        var parameters = new DialogParameters { ["ModpackId"] = ModpackId };
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true };
        await DialogService.ShowAsync<NewsDialog>("Novidades", parameters, options);
    }

    private void OpenServers() => Navigation.NavigateTo($"/modpacks/{ModpackId}/servers");
}
