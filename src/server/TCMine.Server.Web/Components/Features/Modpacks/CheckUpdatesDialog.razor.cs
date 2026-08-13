using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Web.Background;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class CheckUpdatesDialog : IDisposable
{
    private readonly HashSet<string> _selected = [];

    private bool _isChecking = true;

    /// <summary>Acompanhamento desta verificação — uma consulta por mod.</summary>
    private Guid _jobId;
    private string _newVersion = "";
    private List<ModUpdateInfo> _updates = [];

    [Parameter] public Guid SourceVersionId { get; set; }
    [Parameter] public string SourceVersion { get; set; } = "";

    [Inject] private CheckModpackVersionUpdates CheckUseCase { get; set; } = default!;
    [Inject] private CloneVersion CloneUseCase { get; set; } = default!;
    [Inject] private IIngestionQueue IngestionQueue { get; set; } = default!;
    [Inject] private JobProgressRegistry Jobs { get; set; } = default!;

    private JobProgress? Progress => _jobId == Guid.Empty ? null : Jobs.Get(_jobId);

    public void Dispose()
    {
        Jobs.Changed -= OnJobChanged;
        GC.SuppressFinalize(this);
    }

    private void OnJobChanged() => _ = InvokeAsync(StateHasChanged);

    protected override async Task OnInitializedAsync()
    {
        Jobs.Changed += OnJobChanged;
        _jobId = Guid.CreateVersion7();

        var result = await CheckUseCase.HandleAsync(SourceVersionId, CancellationToken.None, _jobId);

        if (result.Succeeded)
        {
            _updates = [.. result.Value!];
            foreach (var u in _updates) // tudo selecionado por padrão
                _selected.Add(u.ProjectSlug);
            _newVersion = SuggestNextVersion(SourceVersion);
        }
        else
            Snackbar.Add(result.Error!, Severity.Error);

        _isChecking = false;
        _jobId = Guid.Empty;
    }

    private void Toggle(string slug, bool selected)
    {
        if (selected) _selected.Add(slug);
        else _selected.Remove(slug);
    }

    private Task Create()
    {
        return RunAsync(async () =>
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
        });
    }

    // Incrementa o último segmento numérico: "7.1" → "7.2", "1.0.3" → "1.0.4".
    private static string SuggestNextVersion(string current)
    {
        var parts = current.Split('.');
        for (var i = parts.Length - 1; i >= 0; i--)
        {
            if (int.TryParse(parts[i], out var n))
            {
                parts[i] = (n + 1).ToString();
                return string.Join('.', parts);
            }
        }

        return string.IsNullOrWhiteSpace(current) ? "1.0" : current + ".1";
    }
}
