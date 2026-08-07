using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class ModSearchDialog
{
    /// <summary>Origens utilizáveis agora (CurseForge só aparece com API key).</summary>
    private readonly List<ModFileOrigin> _available = [];

    private readonly HashSet<string> _selected = [];

    private bool _isSearching;
    private ModFileOrigin _origin = ModFileOrigin.Modrinth;

    private string _query = "";
    private IReadOnlyList<ModSearchResult> _results = [];
    private bool _searched;

    [Parameter] public Guid VersionId { get; set; }
    [Parameter] public string MinecraftVersion { get; set; } = "";
    [Parameter] public ModLoader Loader { get; set; }

    [Inject] private IEnumerable<IModSearch> Searches { get; set; } = default!;
    [Inject] private IIngestionQueue Queue { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        foreach (var search in Searches)
        {
            if (await search.IsAvailableAsync(CancellationToken.None))
                _available.Add(search.Origin);
        }

        // Sem Modrinth configurado seria estranho, mas não presumimos: fica a
        // primeira origem disponível.
        if (!_available.Contains(_origin) && _available.Count > 0)
            _origin = _available[0];
    }

    private async Task OnOriginChanged(ModFileOrigin origin)
    {
        // Resultados de uma origem não valem para outra: limpa e refaz a busca.
        _origin = origin;
        _selected.Clear();
        _results = [];
        _searched = false;

        if (!string.IsNullOrWhiteSpace(_query))
            await DoSearch();
    }

    private async Task OnKeyUp(KeyboardEventArgs e)
    {
        if (e.Key is "Enter")
            await DoSearch();
    }

    private async Task DoSearch()
    {
        if (string.IsNullOrWhiteSpace(_query))
            return;

        var search = Searches.FirstOrDefault(s => s.Origin == _origin);
        if (search is null)
            return;

        _isSearching = true;
        try
        {
            var q = new ModSearchQuery(_query.Trim(), MinecraftVersion, Loader);
            _results = await search.SearchAsync(q, CancellationToken.None);
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

    private Task Add()
    {
        return RunAsync(async () =>
        {
            // O ProjectId da busca vira ProjectSlug do arquivo — identidade
            // estável do mod (slug no Modrinth, id numérico no CurseForge).
            // Lado Both por padrão; a grade permite ajustar depois.
            // FileId null = versão mais recente compatível.
            var items = _results
                .Where(r => _selected.Contains(r.ProjectId))
                .Select(r => new ModIngestionItem(_origin, r.ProjectId, null, FileSide.Both))
                .ToList();

            await Queue.EnqueueAsync(VersionId, items, CancellationToken.None);
            Snackbar.Add($"{items.Count} mod(s) na fila de importação.", Severity.Info);
            Dialog.Close(DialogResult.Ok(true));
        });
    }
}
