using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class ModrinthSearchDialog : ComponentBase
{
    private readonly HashSet<string> _selected = [];
    private bool _isAdding;
    private bool _isSearching;

    private string _query = "";
    private IReadOnlyList<ModSearchResult> _results = [];
    private bool _searched;
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;

    [Parameter] public Guid VersionId { get; set; }
    [Parameter] public string MinecraftVersion { get; set; } = "";
    [Parameter] public ModLoader Loader { get; set; }

    private async Task OnKeyUp(KeyboardEventArgs e)
    {
        if (e.Key is "Enter")
            await DoSearch();
    }

    private async Task DoSearch()
    {
        if (string.IsNullOrWhiteSpace(_query))
            return;

        _isSearching = true;
        try
        {
            var q = new ModSearchQuery(_query.Trim(), MinecraftVersion, Loader);
            _results = await Search.SearchAsync(q, CancellationToken.None);
            _searched = true;
        }
        finally
        {
            _isSearching = false;
        }
    }

    private void Toggle(string projectId, bool selected)
    {
        if (selected)
            _selected.Add(projectId);
        else
            _selected.Remove(projectId);
    }

    private async Task Add()
    {
        _isAdding = true;
        try
        {
            // Slug como ProjectId → vira ProjectSlug no arquivo, identidade
            // estável do mod. Lado Both por padrão; a grade permite ajustar
            // depois. FileId null = versão mais recente compatível.
            var items = _results
                .Where(r => _selected.Contains(r.ProjectId))
                .Select(r => new ModIngestionItem(ModFileOrigin.Modrinth, r.ProjectId, null, FileSide.Both))
                .ToList();

            await Queue.EnqueueAsync(VersionId, items, CancellationToken.None);
            Snackbar.Add($"{items.Count} mod(s) na fila de importação.", Severity.Info);
            Dialog.Close(DialogResult.Ok(true));
        }
        finally
        {
            _isAdding = false;
        }
    }

    private void Cancel() => Dialog.Cancel();
}
