using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Web.Components.Features.Modpacks;
using TCMine.Server.Web.Mapping;

namespace TCMine.Server.Web.Components.Pages.Modpacks;

public partial class ModpacksPage : ComponentBase
{
    private bool _isLoading = true;
    private IReadOnlyList<ModpackDto> _modpacks = [];

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _isLoading = true;

        var entities = await Repository.ListAsync(CancellationToken.None);
        _modpacks = [.. entities.Select(m => m.ToDto())];

        _isLoading = false;
    }

    private async Task Delete(ModpackDto pack)
    {
        var confirm = await DialogService.ShowMessageBoxAsync(
            "Apagar modpack",
            $"Apagar \"{pack.Name}\"? Todas as versões e arquivos serão removidos. Isto é irreversível.",
            "Apagar", cancelText: "Cancelar");
        if (confirm is not true)
            return;

        var result = await DeleteUseCase.HandleAsync(pack.Id, CancellationToken.None);
        if (result.Succeeded)
        {
            Snackbar.Add("Modpack apagado.", Severity.Success);
            await LoadAsync(); // ou o método que recarrega _modpacks
        }
        else
        {
            // A barreira dos servidores volta como mensagem clara aqui.
            Snackbar.Add(result.Error!, Severity.Error);
        }
    }

    private async Task OpenCreateDialog()
    {
        var dialog = await DialogService.ShowAsync<CreateModpackDialog>("Novo modpack");
        var result = await dialog.Result;

        // Recarrega só se o diálogo confirmou a criação. Cancelar não deve
        // custar uma ida ao banco.
        if (result is { Canceled: false })
            await LoadAsync();
    }

    private async Task OpenEditDialog(ModpackDto pack)
    {
        var parameters = new DialogParameters
        {
            ["ModpackId"] = pack.Id,
            ["Name"] = pack.Name,
            ["Summary"] = pack.Summary,
            ["Slug"] = pack.Slug,
            ["MinecraftVersion"] = pack.MinecraftVersion,
            ["Loader"] = pack.Loader
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };

        var dialog = await DialogService.ShowAsync<EditModpackDialog>("Editar modpack", parameters, options);
        if (await dialog.Result is { Canceled: false })
            await LoadAsync();
    }
}
