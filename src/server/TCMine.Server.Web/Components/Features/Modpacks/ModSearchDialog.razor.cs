using Microsoft.AspNetCore.Components;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class ModSearchDialog
{
    /// <summary>
    ///     Valor do seletor que significa "deixa a origem escolher". Vazio, e não
    ///     nulo: o MudSelect trata nulo como "nada selecionado".
    /// </summary>
    private const string LatestCompatible = "";

    /// <summary>Origens utilizáveis agora (CurseForge só aparece com API key).</summary>
    private readonly List<ModFileOrigin> _available = [];

    /// <summary>Versão escolhida por mod marcado (FileId na origem).</summary>
    private readonly Dictionary<string, string> _chosenVersion = [];

    private readonly HashSet<string> _loadingVersions = [];

    private readonly HashSet<string> _selected = [];

    /// <summary>Releases já consultadas, por mod — marcar e desmarcar não reconsulta.</summary>
    private readonly Dictionary<string, IReadOnlyList<UpstreamRelease>> _versions = [];

    /// <summary>
    ///     Esconder os incompatíveis fica LIGADO por padrão: a lista útil é a de
    ///     mods que dá para instalar. Mas o contador ao lado mostra que os outros
    ///     existem — é isso que responde "por que não acho o Mekanism".
    /// </summary>
    private bool _hideIncompatible = true;

    private int _incompatibleCount;

    private bool _isSearching;
    private ModFileOrigin _origin = ModFileOrigin.Modrinth;

    private string _query = "";
    private IReadOnlyList<ModSearchResult> _results = [];
    private bool _searched;

    private IReadOnlyList<ModSearchResult> Visiveis =>
        _hideIncompatible ? [.. _results.Where(r => r.Compatible)] : _results;

    [Parameter] public Guid VersionId { get; set; }
    [Parameter] public string MinecraftVersion { get; set; } = "";
    [Parameter] public ModLoader Loader { get; set; }

    [Inject] private IEnumerable<IModSearch> Searches { get; set; } = null!;
    [Inject] private QueueIngestion QueueIngestionUseCase { get; set; } = null!;

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
        _chosenVersion.Clear();
        _versions.Clear();
        _results = [];
        _searched = false;

        if (!string.IsNullOrWhiteSpace(_query))
            await DoSearch();
    }

    private async Task OnQueryChanged(string value)
    {
        _query = value;

        // Menos de três letras devolve ruído e gasta cota da API à toa.
        if (string.IsNullOrWhiteSpace(_query) || _query.Trim().Length < 3)
        {
            _results = [];
            _searched = false;
            return;
        }

        await DoSearch();
    }

    private void OnHideChanged(bool value) => _hideIncompatible = value;

    /// <summary>1.234.567 → "1,2 mi". Número cheio de download não diz nada de relance.</summary>
    private static string Downloads(int total) => total switch
    {
        >= 1_000_000 => $"{total / 1_000_000d:0.#} mi",
        >= 1_000 => $"{total / 1_000d:0.#} mil",
        _ => total.ToString()
    };

    private string IncompatibleHint(ModSearchResult r) =>
        r.LatestVersions is { Length: > 0 } versions
            ? $"Este mod tem versões para {versions} — nenhuma para {MinecraftVersion} com {Loader}."
            : $"Sem release para Minecraft {MinecraftVersion} com {Loader}.";


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
            _incompatibleCount = _results.Count(r => !r.Compatible);
            _searched = true;
        }
        finally
        {
            _isSearching = false;
        }
    }

    private string ChosenVersion(string projectId) =>
        _chosenVersion.GetValueOrDefault(projectId, LatestCompatible);

    private IReadOnlyList<UpstreamRelease> VersionsOf(string projectId) =>
        _versions.GetValueOrDefault(projectId) ?? [];

    private static string VersionLabel(UpstreamRelease v) =>
        $"{v.Label} · {v.PublishedAt:dd/MM/yyyy}" + (v.IsStable ? "" : " · beta/alpha");

    /// <summary>Consulta as releases compatíveis de um mod, uma vez por mod.</summary>
    private async Task LoadVersionsAsync(string projectId)
    {
        if (_versions.ContainsKey(projectId) || !_loadingVersions.Add(projectId))
            return;

        try
        {
            var search = Searches.FirstOrDefault(s => s.Origin == _origin);
            _versions[projectId] = search is null
                ? []
                : await search.ListVersionsAsync(projectId, MinecraftVersion, Loader, CancellationToken.None);
        }
        finally
        {
            _loadingVersions.Remove(projectId);
        }
    }

    private async Task Toggle(string projectId, bool selected)
    {
        switch (selected)
        {
            // O card inteiro é clicável, então a guarda precisa estar aqui e não só
            // no Checkbox desabilitado: marcar um incompatível só adiaria a recusa
            // para a ingestão, com o admin achando que deu certo.
            case true when _results.Any(r => r.ProjectId == projectId && !r.Compatible):
                return;
            case false:
                _selected.Remove(projectId);
                return;
            default:
                _selected.Add(projectId);
                await LoadVersionsAsync(projectId);
                break;
        }
    }

    private Task Add()
    {
        // O ProjectId da busca vira ProjectSlug do arquivo — identidade
        // estável do mod (slug no Modrinth, id numérico no CurseForge).
        // Lado Both por padrão; a grade permite ajustar depois.
        // FileId null = versão mais recente compatível; com versão escolhida, a
        // ingestão fixa exatamente ela (e a procura primeiro no banco/disco).
        var items = _results
            .Where(r => _selected.Contains(r.ProjectId))
            .Select(r => new ModIngestionItem(
                _origin,
                r.ProjectId,
                ChosenVersion(r.ProjectId) is { Length: > 0 } fileId ? fileId : null,
                FileSide.Both))
            .ToList();

        // Passa pelo QueueIngestion (via IngestionScheduler), não pela fila
        // direto: ele grava a pendência Queued ANTES de enfileirar, para o
        // pedido sobreviver a uma queda do processo entre o clique e o worker
        // pegar o item.
        return SubmitAsync(
            () => QueueIngestionUseCase.HandleAsync(new QueueIngestionCommand(VersionId, items),
                CancellationToken.None),
            $"{items.Count} mod(s) na fila de importação.");
    }
}
