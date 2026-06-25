using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine_Domain.Entities;
using TCMine_Infrastructure.Server;
using TCMine_Server.Components.Pages.Admin.Modpacks.Dialogs;

namespace TCMine_Server.Components.Pages.Admin.Modpacks;

/// <summary>
/// Parte do editor dedicada à <b>newsletter do modpack</b>. Como os overrides, grava direto no banco
/// (conteúdo independente do rascunho), por isso só fica disponível depois do primeiro Guardar —
/// antes disso o modpack ainda não existe para amarrar a FK.
/// </summary>
public partial class ModpackEditor
{
    [Inject] private ModpackNewsService NewsService { get; set; } = null!;

    private List<NewsEntity>? _news;

    private async Task ReloadNewsAsync()
    {
        _news = await NewsService.ListForModpackAsync(_draft.Id);
    }

    private async Task NewNewsAsync()
    {
        await EditNewsAsync(null);
    }

    // Cria (entry == null) ou edita uma notícia via diálogo; persiste no confirmar.
    private async Task EditNewsAsync(NewsEntity? entry)
    {
        var parameters = new DialogParameters<NewsEditDialog>
        {
            { x => x.Entry, entry }
        };
        var dialog = await DialogService.ShowAsync<NewsEditDialog>(
            entry is null ? "Nova novidade" : "Editar novidade", parameters,
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = true });

        var result = await dialog.Result;
        if (result is null || result.Canceled || result.Data is not NewsEntity draft) return;

        try
        {
            await Busy.RunAsync("Salvando novidade…", async () =>
            {
                if (entry is null)
                    await NewsService.CreateAsync(_draft.Id, draft);
                else
                    await NewsService.UpdateAsync(draft);

                await ReloadNewsAsync();
            });
            Snackbar.Add("Novidade salva.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha ao salvar novidade: {ex.Message}", Severity.Error);
        }
    }

    private async Task DeleteNewsAsync(NewsEntity entry)
    {
        var ok = await DialogService.ShowMessageBoxAsync(
            "Apagar novidade", $"Apagar \"{entry.Title}\"?", "Apagar", cancelText: "Cancelar");
        if (ok != true) return;

        try
        {
            await Busy.RunAsync("Apagando novidade…", async () =>
            {
                await NewsService.DeleteAsync(entry.Id);
                await ReloadNewsAsync();
            });
            Snackbar.Add("Novidade apagada.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha ao apagar: {ex.Message}", Severity.Error);
        }
    }
}