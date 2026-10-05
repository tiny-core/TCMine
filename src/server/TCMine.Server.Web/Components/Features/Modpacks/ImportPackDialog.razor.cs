using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class ImportPackDialog
{
    private bool _isSearching;
    private string _query = "";
    private IReadOnlyList<UpstreamPackSummary> _results = [];
    private bool _searched;
    private UpstreamPackSummary? _selected;

    /// <summary>Releases do pack selecionado, da mais nova para a mais velha.</summary>
    private IReadOnlyList<UpstreamRelease> _releases = [];

    private bool _loadingReleases;

    /// <summary>Release escolhida; nula = deixa a origem decidir (a mais recente).</summary>
    private string? _releaseId;

    /// <summary>Origens prontas para uso — o Modrinth sempre está, o CurseForge só com chave.</summary>
    private List<IUpstreamPackSource> _available = [];

    /// <summary>Nulo enquanto nenhuma origem estiver disponível.</summary>
    private IUpstreamPackSource? _source;

    private ModFileOrigin _originValue;

    [Inject] private IEnumerable<IUpstreamPackSource> Sources { get; set; } = default!;
    [Inject] private ImportScheduler Scheduler { get; set; } = default!;
    [Inject] private IModpackRepository Repository { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        foreach (var candidate in Sources)
        {
            if (await candidate.IsAvailableAsync(CancellationToken.None))
                _available.Add(candidate);
        }

        // Modrinth primeiro por padrão: não precisa de chave, e lá nenhum autor
        // pode negar redistribuição — a importação sai inteira.
        _available = [.. _available.OrderBy(s => s.Origin is ModFileOrigin.Modrinth ? 0 : 1)];

        _source = _available.FirstOrDefault();
        _originValue = _source?.Origin ?? ModFileOrigin.Modrinth;
    }

    private void OnOriginChanged(ModFileOrigin origin)
    {
        _originValue = origin;
        _source = _available.FirstOrDefault(s => s.Origin == origin);

        // Resultado de uma origem não vale para a outra.
        _results = [];
        _searched = false;
        ClearSelection();
    }

    private void ClearSelection()
    {
        _selected = null;
        _releases = [];
        _releaseId = null;
    }

    private static string ReleaseLabel(UpstreamRelease r)
    {
        var label = $"{r.Label} · {r.PublishedAt:dd/MM/yyyy}";
        if (r.MinecraftVersions is { Length: > 0 } mc)
            label += $" · MC {mc}";
        return r.IsStable ? label : label + " · beta/alpha";
    }

    /// <summary>
    ///     Seleciona o pack e lista as releases dele. A pré-seleção é a mais
    ///     recente ESTÁVEL — a mesma regra que a importação sem escolha usa —, e
    ///     não simplesmente a primeira da lista, que pode ser uma alpha.
    /// </summary>
    private async Task SelectAsync(UpstreamPackSummary pack)
    {
        if (_source is null || _selected?.ProjectId == pack.ProjectId)
            return;

        _selected = pack;
        _releases = [];
        _releaseId = null;
        _loadingReleases = true;
        try
        {
            var releases = await _source.ListReleasesAsync(pack.ProjectId, CancellationToken.None);

            // Outro pack pode ter sido clicado enquanto este carregava.
            if (_selected?.ProjectId != pack.ProjectId)
                return;

            _releases = releases;
            _releaseId = (releases.FirstOrDefault(r => r.IsStable) ?? (releases.Count > 0 ? releases[0] : null))?.FileId;
        }
        catch (HttpRequestException)
        {
            // Sem lista, importa-se a mais recente — o mesmo de antes do seletor.
            _releases = [];
        }
        finally
        {
            _loadingReleases = false;
        }
    }

    private async Task OnKeyUp(KeyboardEventArgs e)
    {
        if (e.Key is "Enter")
            await DoSearch();
    }

    private async Task DoSearch()
    {
        if (_source is null || string.IsNullOrWhiteSpace(_query))
            return;

        _isSearching = true;
        try
        {
            _results = await _source.SearchPacksAsync(_query.Trim(), 20, CancellationToken.None);
            _searched = true;
            ClearSelection();
        }
        finally
        {
            _isSearching = false;
        }
    }

    private Task Import()
    {
        if (_source is null || _selected is null)
            return Task.CompletedTask;

        var origin = _source.Origin;
        var projectId = _selected.ProjectId;
        var name = _selected.Name;
        var fileId = _releaseId;

        // A importação inteira vai para a fila: baixar o zip de um pack grande e
        // gravar milhares de overrides leva minutos, e prender o diálogo até o
        // fim passa a impressão de que o sistema travou. Não dá para criar o
        // Modpack antes e só enfileirar o resto — nome, versão do Minecraft e
        // loader só se sabem depois de ler o manifest de dentro do zip.
        return RunAsync(async () =>
        {
            if (await Repository.ExistsFromUpstreamAsync(origin, projectId, CancellationToken.None))
            {
                Snackbar.Add($"'{name}' já foi importado.", Severity.Warning);
                return;
            }

            // fileId null = release mais recente do pack.
            var result = await Scheduler.ScheduleAsync(origin, projectId, fileId, name, CancellationToken.None);

            if (!result.Succeeded)
            {
                Snackbar.Add(result.Error!, Severity.Warning);
                return;
            }

            Snackbar.Add(
                $"Importando '{name}'. Acompanhe o progresso na barra do topo.",
                Severity.Success);

            Dialog.Close(DialogResult.Ok(true));
        });
    }
}
