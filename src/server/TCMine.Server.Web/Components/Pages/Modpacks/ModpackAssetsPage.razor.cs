using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Web.Components.Features.Modpacks;

namespace TCMine.Server.Web.Components.Pages.Modpacks;

/// <summary>
///     Shaderpacks, resource packs e data packs de uma versão.
///     Aba própria porque não se gerenciam como mod: não têm atualização a
///     verificar, dependência a resolver nem lado a discutir — vão para o
///     jogador. Misturados na grade de mods, sumiam no meio de centenas de
///     linhas, e enviar um exigia adivinhar a pasta no diálogo genérico.
/// </summary>
public partial class ModpackAssetsPage
{
    private MudDataGrid<ModpackFile> _grid = default!;
    private bool _isLoading = true;
    private Modpack? _modpack;
    private ModpackVersion? _version;

    [Parameter] public Guid ModpackId { get; set; }
    [Parameter] public Guid VersionId { get; set; }

    protected override Task OnParametersSetAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        _isLoading = true;

        _modpack = await Repository.GetWithVersionsAsync(ModpackId, CancellationToken.None);
        _version = _modpack?.Versions.FirstOrDefault(v => v.Id == VersionId);

        _isLoading = false;

        if (_grid is not null)
            await _grid.ReloadServerData();
    }

    private async Task<GridData<ModpackFile>> LoadPageAsync(
        GridState<ModpackFile> state, CancellationToken ct)
    {
        // O grid fica carregando PARA SEMPRE se isto estourar — não há estado de
        // erro nele. Uma consulta que o provider não traduz vira um spinner
        // eterno, sem uma linha na tela dizendo o que houve, e foi assim que a
        // aba de recursos apareceu quebrada.
        try
        {
            var result = await Repository.ListVersionFilesAsync(
                VersionId,
                VersionFileScope.Assets,
                null,
                new PageRequest(state.Page, state.PageSize),
                ct);

            return new GridData<ModpackFile> { Items = result.Items, TotalItems = result.TotalCount };
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Não foi possível listar os recursos: {ex.Message}", Severity.Error);
            return new GridData<ModpackFile> { Items = [], TotalItems = 0 };
        }
    }

    private void OnVersionChanged(Guid versionId) =>
        Navigation.NavigateTo($"/modpacks/{ModpackId}/versions/{versionId}/recursos");

    private async Task OpenUpload()
    {
        // Já abre em shaderpacks/ e como só-cliente: é o caso desta aba, e
        // repetir a escolha a cada envio é convidar ao erro que manda um shader
        // para o container do servidor.
        var parameters = new DialogParameters
        {
            ["VersionId"] = VersionId,
            ["DefaultFolder"] = InstanceFolders.Shaderpacks,
            ["DefaultSide"] = FileSide.ClientOnly
        };

        var dialog = await DialogService.ShowAsync<ManualUploadDialog>("Enviar arquivo", parameters);

        if (await dialog.Result is { Canceled: false })
            await LoadAsync();
    }

    private async Task Remove(ModpackFile file)
    {
        var confirm = await DialogService.ShowMessageBoxAsync(
            "Remover arquivo",
            $"Remover '{file.Path}' desta versão?",
            "Remover", cancelText: "Cancelar");

        if (confirm is not true)
            return;

        var result = await RemoveFileUseCase.HandleAsync(VersionId, file.Id, CancellationToken.None);

        if (result.Succeeded)
        {
            Snackbar.Add("Arquivo removido.", Severity.Success);
            await LoadAsync();
        }
        else
        {
            Snackbar.Add(result.Error!, Severity.Error);
        }
    }
}
