using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine_Application.Contracts;
using TCMine_Infrastructure.Minecraft;

namespace TCMine_Server.Components.Pages.Admin.Modpacks;

/// <summary>
/// Catálogo de modpacks do painel. Só lista, navega para o editor e apaga — toda a edição
/// (mods, overrides, servidores) vive no <see cref="ModpackEditor"/>. Carrega as linhas uma vez
/// no init; apagar remove da BD e atualiza a lista em memória sem recarregar tudo.
/// </summary>
public partial class Modpacks : ComponentBase
{
    [Inject] private ModpackImportService Service { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    // null = carregando (mostra skeletons); lista vazia = estado vazio
    private List<ModpackAdminRowDto>? _rows;
    private bool _cfConfigured = true;

    protected override async Task OnInitializedAsync()
    {
        _cfConfigured = await Service.IsCfConfiguredAsync();
        _rows = await Service.ListAsync();
    }

    private void Edit(ModpackAdminRowDto row)
    {
        Nav.NavigateTo($"/admin/modpacks/{row.Id}");
    }

    private async Task DeleteAsync(ModpackAdminRowDto row)
    {
        var ok = await DialogService.ShowMessageBoxAsync(
            "Apagar modpack",
            $"Apagar \"{row.Name}\"? Os mods continuam no cache compartilhado, mas os overrides deste modpack serão removidos.",
            yesText: "Apagar", cancelText: "Cancelar");

        if (ok != true) return;

        try
        {
            await Service.DeleteAsync(row.Id);
            _rows?.Remove(row);
            Snackbar.Add("Modpack apagado.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha ao apagar: {ex.Message}", Severity.Error);
        }
    }
}
