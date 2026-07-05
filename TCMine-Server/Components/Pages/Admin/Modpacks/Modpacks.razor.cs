using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine_Application.Contracts;
using TCMine_Server.Infrastructure.Minecraft;
using TCMine_Server.Services;

namespace TCMine_Server.Components.Pages.Admin.Modpacks;

/// <summary>
///     Catálogo de modpacks do painel. Só lista, navega para o editor e apaga — toda a edição
///     (mods, overrides, servidores) vive no <see cref="ModpackEditor" />. Carrega as linhas uma vez
///     no init; apagar remove da BD e atualiza a lista em memória sem recarregar tudo.
/// </summary>
public partial class Modpacks : ComponentBase
{
    private bool _cfConfigured = true;

    // null = carregando (BusyOverlay cobre a tela); lista vazia = estado vazio
    private List<ModpackAdminRowDto>? _rows;

    // Filtro textual (o MudDataGrid pagina/filtra; QuickFilter combina com a busca)
    private string _search = string.Empty;
    [Inject] private ModpackImportService Service { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private BusyService Busy { get; set; } = null!;

    // QuickFilter do DataGrid: nome ou versão do Minecraft
    private bool Filter(ModpackAdminRowDto row)
    {
        if (string.IsNullOrWhiteSpace(_search)) return true;
        return row.Name.Contains(_search, StringComparison.OrdinalIgnoreCase)
               || row.Minecraft.Contains(_search, StringComparison.OrdinalIgnoreCase);
    }

    protected override async Task OnInitializedAsync()
    {
        await Busy.RunAsync("Carregando modpacks…", async () =>
        {
            _cfConfigured = await Service.IsCfConfiguredAsync();
            _rows = await Service.ListAsync();
        });
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
            "Apagar", cancelText: "Cancelar");

        if (ok != true) return;

        try
        {
            await Busy.RunAsync("Apagando modpack…", () => Service.DeleteAsync(row.Id));
            _rows?.Remove(row);
            Snackbar.Add("Modpack apagado.", Severity.Success);
        }
        catch (InvalidOperationException ex)
        {
            // Regra de negócio (ex.: servidores atrelados) — mensagem já é clara e acionável
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha ao apagar: {ex.Message}", Severity.Error);
        }
    }
}