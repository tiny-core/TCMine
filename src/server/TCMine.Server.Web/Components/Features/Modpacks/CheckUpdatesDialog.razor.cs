using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Application.Modpacks;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class CheckUpdatesDialog : ComponentBase
{
    private readonly HashSet<string> _selected = [];

    private bool _isChecking = true;
    private bool _isCreating;
    private string _newVersion = "";
    private List<ModUpdateInfo> _updates = [];
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;

    [Parameter] public Guid SourceVersionId { get; set; }
    [Parameter] public string SourceVersion { get; set; } = "";

    protected override async Task OnInitializedAsync()
    {
        var result = await CheckUseCase.HandleAsync(SourceVersionId, CancellationToken.None);

        if (result.Succeeded)
        {
            _updates = [.. result.Value!];
            foreach (var u in _updates) // tudo selecionado por padrão
                _selected.Add(u.ProjectSlug);
            _newVersion = SuggestNextVersion(SourceVersion);
        }
        else
        {
            Snackbar.Add(result.Error!, Severity.Error);
        }

        _isChecking = false;
    }

    private void Toggle(string slug, bool selected)
    {
        if (selected) _selected.Add(slug);
        else _selected.Remove(slug);
    }

    private async Task Create()
    {
        _isCreating = true;
        try
        {
            // 1. Clona a versão publicada num Draft novo (arquivos copiados).
            var clone = await CloneUseCase.HandleAsync(SourceVersionId, _newVersion, CancellationToken.None);
            if (!clone.Succeeded)
            {
                Snackbar.Add(clone.Error!, Severity.Error);
                return;
            }

            var newVersionId = clone.Value;

            // 2. Re-ingere só os mods escolhidos no Draft novo. FileId null =
            //    versão mais recente; UpsertFile troca o .jar mantendo o slug.
            var items = _updates
                .Where(u => _selected.Contains(u.ProjectSlug))
                .Select(u => new ModIngestionItem(u.Origin, u.ProjectSlug, null, u.Side))
                .ToList();

            await IngestionQueue.EnqueueAsync(newVersionId, items, CancellationToken.None);

            Snackbar.Add($"Versão {_newVersion} criada; atualizando {items.Count} mod(s)…", Severity.Success);
            Dialog.Close(DialogResult.Ok(newVersionId));
        }
        finally
        {
            _isCreating = false;
        }
    }

    private void Close()
    {
        Dialog.Cancel();
    }

    // Incrementa o último segmento numérico: "7.1" → "7.2", "1.0.3" → "1.0.4".
    private static string SuggestNextVersion(string current)
    {
        var parts = current.Split('.');
        for (var i = parts.Length - 1; i >= 0; i--)
            if (int.TryParse(parts[i], out var n))
            {
                parts[i] = (n + 1).ToString();
                return string.Join('.', parts);
            }

        return string.IsNullOrWhiteSpace(current) ? "1.0" : current + ".1";
    }
}