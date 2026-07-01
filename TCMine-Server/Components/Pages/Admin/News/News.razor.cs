using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine_Application.Contracts;
using TCMine_Domain.Entities;
using TCMine_Server.Infrastructure.Server;
using TCMine_Server.Components.Pages.Admin.Modpacks.Dialogs;
using TCMine_Server.Services;

namespace TCMine_Server.Components.Pages.Admin.News;

/// <summary>
/// Página de novidades do painel: lista globais + de modpacks (<see cref="ModpackNewsService"/>).
/// Criar/editar abre o <c>NewsEditDialog</c> com o seletor de modpack opcional — vazio = global,
/// selecionado = do modpack. Segue o padrão de listas (MudDataGrid + busca + pager).
/// </summary>
public partial class News : ComponentBase
{
    [Inject] private ModpackNewsService NewsService { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private BusyService Busy { get; set; } = null!;

    // null = carregando (overlay cobre); lista vazia = sem novidades
    private List<NewsRowDto>? _rows;

    // Opções do seletor de modpack no diálogo (carregadas uma vez)
    private List<ModpackBadgeDto> _modpacks = [];

    private string _search = string.Empty;

    // QuickFilter do DataGrid: título ou tag
    private bool Filter(NewsRowDto row)
    {
        if (string.IsNullOrWhiteSpace(_search)) return true;
        return row.Title.Contains(_search, StringComparison.OrdinalIgnoreCase)
               || row.Tag.Contains(_search, StringComparison.OrdinalIgnoreCase);
    }

    protected override async Task OnInitializedAsync()
    {
        await Busy.RunAsync("Carregando novidades…", async () =>
        {
            _modpacks = await NewsService.ListModpackOptionsAsync();
            await ReloadAsync();
        });
    }

    private async Task ReloadAsync()
    {
        _rows = await NewsService.ListAllAsync();
    }

    private Task NewAsync()
    {
        return EditAsync(null);
    }

    // Cria (row == null) ou edita uma novidade via diálogo (com seletor de modpack); persiste no confirmar.
    private async Task EditAsync(NewsRowDto? row)
    {
        // Entry para o diálogo: null = criar; reconstrói a entidade a partir da linha para editar
        var entry = row is null
            ? null
            : new NewsEntity
            {
                Id = row.Id, ModpackId = row.ModpackId, Tag = row.Tag, Title = row.Title,
                Summary = row.Summary, PublishedAt = row.PublishedAt, IsPublished = row.IsPublished
            };

        var parameters = new DialogParameters<NewsEditDialog>
        {
            { x => x.Entry, entry },
            { x => x.Modpacks, _modpacks }
        };
        var dialog = await DialogService.ShowAsync<NewsEditDialog>(
            row is null ? "Nova novidade" : "Editar novidade", parameters,
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = true });

        var result = await dialog.Result;
        if (result is null || result.Canceled || result.Data is not NewsEntity draft) return;

        try
        {
            await Busy.RunAsync("Salvando novidade…", async () =>
            {
                if (row is null)
                    await NewsService.CreateAsync(draft);
                else
                    await NewsService.UpdateAsync(draft);

                await ReloadAsync();
            });
            Snackbar.Add("Novidade salva.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha ao salvar: {ex.Message}", Severity.Error);
        }
    }

    private async Task DeleteAsync(NewsRowDto row)
    {
        var ok = await DialogService.ShowMessageBoxAsync(
            "Apagar novidade", $"Apagar \"{row.Title}\"?", "Apagar", cancelText: "Cancelar");
        if (ok != true) return;

        try
        {
            await Busy.RunAsync("Apagando novidade…", async () =>
            {
                await NewsService.DeleteAsync(row.Id);
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
