using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine_Domain.Entities;
using TCMine_Infrastructure.Server;
using TCMine_Server.Components.Pages.Admin.Modpacks.Dialogs;
using TCMine_Server.Services;

namespace TCMine_Server.Components.Pages.Admin.Modpacks;

/// <summary>
/// Aba "Novidades" do <see cref="ModpackEditor"/>: newsletter por modpack. Self-contained — carrega
/// a lista do <see cref="ModpackNewsService"/> e faz o CRUD direto (grava na hora, fora da política
/// de escrita-só-ao-Guardar). Só é renderizado depois do primeiro Guardar (o editor garante o id).
/// </summary>
public partial class NewsPanel : ComponentBase
{
    [Parameter] [EditorRequired] public Guid ModpackId { get; set; }

    [Inject] private ModpackNewsService NewsService { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private BusyService Busy { get; set; } = null!;

    // null = carregando (BusyOverlay cobre); lista vazia = sem novidades
    private List<NewsEntity>? _news;

    // Filtro textual (o MudDataGrid pagina; QuickFilter combina com a busca)
    private string _search = string.Empty;

    // QuickFilter do DataGrid: título ou tag
    private bool Filter(NewsEntity n)
    {
        if (string.IsNullOrWhiteSpace(_search)) return true;
        return n.Title.Contains(_search, StringComparison.OrdinalIgnoreCase)
               || n.Tag.Contains(_search, StringComparison.OrdinalIgnoreCase);
    }

    protected override async Task OnInitializedAsync()
    {
        await Busy.RunAsync("Carregando novidades…", ReloadAsync);
    }

    private async Task ReloadAsync()
    {
        _news = await NewsService.ListForModpackAsync(ModpackId);
    }

    private Task NewAsync()
    {
        return EditAsync(null);
    }

    // Cria (entry == null) ou edita uma notícia via diálogo; persiste no confirmar.
    private async Task EditAsync(NewsEntity? entry)
    {
        var parameters = new DialogParameters<NewsEditDialog> { { x => x.Entry, entry } };
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
                    await NewsService.CreateAsync(ModpackId, draft);
                else
                    await NewsService.UpdateAsync(draft);

                await ReloadAsync();
            });
            Snackbar.Add("Novidade salva.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha ao salvar novidade: {ex.Message}", Severity.Error);
        }
    }

    private async Task DeleteAsync(NewsEntity entry)
    {
        var ok = await DialogService.ShowMessageBoxAsync(
            "Apagar novidade", $"Apagar \"{entry.Title}\"?", "Apagar", cancelText: "Cancelar");
        if (ok != true) return;

        try
        {
            await Busy.RunAsync("Apagando novidade…", async () =>
            {
                await NewsService.DeleteAsync(entry.Id);
                await ReloadAsync();
            });
            Snackbar.Add("Novidade apagada.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha ao apagar: {ex.Message}", Severity.Error);
        }
    }
}
