using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Web.Components.Features.Modpacks;

namespace TCMine.Server.Web.Components.Pages.Modpacks;

public partial class ModpackDetailPage : ComponentBase
{
    private bool _isLoading = true;
    private Modpack? _modpack;
    private IReadOnlyList<ModpackVersion> _versions = [];

    [Parameter] public Guid ModpackId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;

        _modpack = await Repository.GetByIdAsync(ModpackId, CancellationToken.None);

        if (_modpack is not null)
            _versions = await Repository.ListVersionsAsync(ModpackId, CancellationToken.None);

        _isLoading = false;
    }

    private async Task OpenCreateVersionDialog()
    {
        var parameters = new DialogParameters { ["ModpackId"] = ModpackId };
        var dialog = await DialogService.ShowAsync<CreateVersionDialog>("Nova versão", parameters);
        var result = await dialog.Result;

        if (result is { Canceled: false })
            await LoadAsync();
    }

    private void OpenVersion(ModpackVersion version)
    {
        // A gestão de arquivos e a publicação vivem num diálogo próprio,
        // aberto ao clicar na versão. Mantém esta página focada na lista.
        _ = OpenVersionDetailAsync(version);
    }

    private async Task OpenVersionDetailAsync(ModpackVersion version)
    {
        var parameters = new DialogParameters { ["VersionId"] = version.Id };
        var dialog = await DialogService.ShowAsync<VersionDetailDialog>(
            $"Versão {version.Version}", parameters);
        var result = await dialog.Result;

        // Recarrega sempre: o diálogo pode ter adicionado arquivos ou
        // publicado, e a lista precisa refletir o novo estado.
        if (result is not null)
            await LoadAsync();
    }
}