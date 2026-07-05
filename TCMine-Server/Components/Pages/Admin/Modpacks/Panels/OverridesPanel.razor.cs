using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine_Server.Components.Pages.Admin.Modpacks.Dialogs;
using TCMine_Server.Components.Shared;
using TCMine_Server.Infrastructure.Minecraft;
using TCMine_Server.Services;

namespace TCMine_Server.Components.Pages.Admin.Modpacks.Panels;

/// <summary>
///     Painel de overrides de um modpack: hoje um wrapper fino sobre o <see cref="FileTreeEditor" />
///     (árvore + Monaco compartilhados). Adiciona o que é específico dos overrides — o histórico/desfazer —
///     via o slot de toolbar, e mantém o estado <c>_hasHistory</c> em sync pelo callback <c>OnChanged</c>.
/// </summary>
public partial class OverridesPanel : ComponentBase
{
    private bool _hasHistory;
    private IFileTreeSource _source = null!;

    private FileTreeEditor _tree = null!;
    [Parameter] public Guid ModpackId { get; set; }

    [Inject] private ModpackOverridesService Service { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private BusyService Busy { get; set; } = null!;

    protected override void OnParametersSet()
    {
        _source = new OverridesTreeSource(Service, ModpackId);
    }

    // Chamado pelo FileTreeEditor após carregar/operações — reavalia se há o que desfazer
    private async Task RefreshHistoryAsync()
    {
        _hasHistory = await Service.GetLastHistoryAsync(ModpackId) is not null;
    }

    private async Task UndoLastAsync()
    {
        try
        {
            var undone = await Busy.RunAsync("Desfazendo…", () => Service.UndoLastAsync(ModpackId));
            if (undone is null)
            {
                Snackbar.Add("Nada para desfazer.", Severity.Info);
                return;
            }

            await _tree.RefreshAsync(); // reconstrói a árvore (a estrutura no disco mudou)
            await RefreshHistoryAsync();
            Snackbar.Add("Última ação desfeita.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha ao desfazer: {ex.Message}", Severity.Error);
        }
    }

    private async Task ShowHistoryAsync()
    {
        var parameters = new DialogParameters<OverrideHistoryDialog> { { x => x.ModpackId, ModpackId } };
        var dialog = await DialogService.ShowAsync<OverrideHistoryDialog>(
            "Histórico de overrides", parameters,
            new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, CloseButton = true });

        var result = await dialog.Result;
        if (result is null || result.Canceled) return;

        // O diálogo pode ter revertido várias ações — reconstrói a árvore e o estado
        await _tree.RefreshAsync();
        await RefreshHistoryAsync();
    }
}