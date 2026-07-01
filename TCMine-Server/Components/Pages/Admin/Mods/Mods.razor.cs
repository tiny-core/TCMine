using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine_Application.Contracts;
using TCMine_Server.Infrastructure.Minecraft;
using TCMine_Server.Services;

namespace TCMine_Server.Components.Pages.Admin.Mods;

/// <summary>
/// Catálogo de todos os arquivos de mod do servidor (um por FileId, compartilhados entre modpacks),
/// com os modpacks em que cada um aparece e o marcador de órfão. Só leitura, exceto a limpeza de
/// arquivos órfãos (sem vínculo). Carrega uma vez no init; recarrega após apagar um órfão.
/// </summary>
public partial class Mods : ComponentBase
{
    [Inject] private ModpackImportService Service { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private BusyService Busy { get; set; } = null!;

    // null = carregando (BusyOverlay cobre a tela); lista vazia = estado vazio
    private List<ModFileRowDto>? _rows;

    private string _search = string.Empty;
    private bool _onlyOrphans;

    private int _orphanCount;

    protected override async Task OnInitializedAsync()
    {
        await Busy.RunAsync("Carregando mods…", ReloadAsync);
    }

    private async Task ReloadAsync()
    {
        _rows = await Service.ListModFilesAsync();
        _orphanCount = _rows.Count(r => r.IsOrphan);
    }

    // QuickFilter do DataGrid: combina o switch "só órfãos" com a busca textual (nome/arquivo)
    private bool Filter(ModFileRowDto row)
    {
        if (_onlyOrphans && !row.IsOrphan) return false;
        if (string.IsNullOrWhiteSpace(_search)) return true;

        return row.Name.Contains(_search, StringComparison.OrdinalIgnoreCase)
               || row.FileName.Contains(_search, StringComparison.OrdinalIgnoreCase);
    }

    private void OpenModpack(Guid modpackId)
    {
        Nav.NavigateTo($"/admin/modpacks/{modpackId}");
    }

    private async Task DeleteOrphanAsync(ModFileRowDto row)
    {
        var ok = await DialogService.ShowMessageBoxAsync(
            "Apagar mod órfão",
            $"Apagar \"{row.Name}\" do servidor? O jar sairá do cache. Esta ação não pode ser desfeita.",
            "Apagar", cancelText: "Cancelar");

        if (ok != true) return;

        try
        {
            await Busy.RunAsync("Apagando mod…", async () =>
            {
                var deleted = await Service.DeleteOrphanFileAsync(row.FileId);
                if (!deleted)
                    // Passou a estar vinculado entre o load e o clique — recarrega para refletir
                    Snackbar.Add("Mod não está mais órfão; lista atualizada.", Severity.Warning);

                await ReloadAsync();
            });
            Snackbar.Add("Mod apagado.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha ao apagar: {ex.Message}", Severity.Error);
        }
    }

    // Formata bytes em KB/MB (1 casa) para a coluna de tamanho
    private static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "—";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} KB";
        return $"{bytes / (1024.0 * 1024.0):0.#} MB";
    }
}