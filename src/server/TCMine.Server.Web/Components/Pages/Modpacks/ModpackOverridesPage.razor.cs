using BlazorMonaco.Editor;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Web.Components.Features.Modpacks;

namespace TCMine.Server.Web.Components.Pages.Modpacks;

public partial class ModpackOverridesPage
{
    private bool? _appliedDarkMode;
    private bool _dirty;

    private string? _dragPath; // path a ser arrastado (definido no handle)
    private string? _dropTarget; // path sobre o qual se está a pairar (para realce)
    private StandaloneCodeEditor _editor = default!;

    private bool _editorReady;
    private bool _isLoading = true;
    private bool _isSaving;

    /// <summary>Leitura do arquivo em curso — o clique numa árvore grande não é instantâneo.</summary>
    private bool _isOpening;

    /// <summary>Preenchido quando o arquivo aberto não cabe no editor (binário ou grande).</summary>
    private OverrideContent? _notEditable;
    private Modpack? _modpack;
    private string? _selectedPath;
    private List<TreeItemData<string>> _treeItems = [];

    /// <summary>
    ///     A árvore inteira em memória (barata: são strings), indexada por caminho.
    ///     Só vira componente o galho que o admin abre — ver LoadChildrenAsync.
    /// </summary>
    private Dictionary<string, Node> _nodesByPath = new(StringComparer.OrdinalIgnoreCase);
    private int _treeRevision;
    private ModpackVersion? _version;
    [Parameter] public Guid ModpackId { get; set; }
    [Parameter] public Guid VersionId { get; set; }

    [Inject] private IModpackRepository Repository { get; set; } = default!;
    [Inject] private ReadOverride ReadUseCase { get; set; } = default!;
    [Inject] private SaveOverride SaveUseCase { get; set; } = default!;
    [Inject] private DeleteOverride DeleteUseCase { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private MoveOverride MoveUseCase { get; set; } = default!;

    [Inject] private UndoOverrideMove UndoUseCase { get; set; } = default!;
    [Inject] private OverrideUndoService UndoService { get; set; } = default!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    // Tema do app, cascateado pelo MainLayout. O Monaco monta fora do
    // MudThemeProvider, então precisa deste sinal para casar claro/escuro.
    [CascadingParameter(Name = "IsDarkMode")]
    private bool IsDarkMode { get; set; }

    // Nome do tema Monaco correspondente ao tema atual do app.
    private string MonacoTheme => IsDarkMode ? "vs-dark" : "vs";

    protected override async Task OnInitializedAsync() => await LoadAsync();

    // Trocar versão numa aba por versão navega para a mesma aba da nova. Como o
    // Monaco quebra com enhanced navigation, força recarregar (forceLoad).
    private void OnVersionChanged(Guid versionId) =>
        Navigation.NavigateTo($"/modpacks/{ModpackId}/versions/{versionId}/overrides", true);

    private async Task LoadAsync()
    {
        _isLoading = true;

        // Modpack com versões (para o seletor do workspace) + a versão da rota.
        _modpack = await Repository.GetWithVersionsAsync(ModpackId, CancellationToken.None);
        _version = _modpack?.Versions.FirstOrDefault(v => v.Id == VersionId);

        BuildTree();
        _isLoading = false;
    }

    // Monta a árvore aninhada a partir dos caminhos planos dos overrides.
    private void BuildTree()
    {
        var paths = (_version?.Files ?? [])
            .Where(f => f.Origin == ModFileOrigin.Override)
            .Select(f => f.Path)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);

        var root = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var level = root;
            var acc = "";
            for (var i = 0; i < segments.Length; i++)
            {
                acc = acc.Length == 0 ? segments[i] : $"{acc}/{segments[i]}";
                if (!level.TryGetValue(segments[i], out var node))
                {
                    node = new Node { Name = segments[i], FullPath = acc, IsFile = i == segments.Length - 1 };
                    level[segments[i]] = node;
                }

                level = node.Children;
            }
        }

        _nodesByPath = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
        IndexNodes(root.Values);

        _treeItems = [.. root.Values.OrderBy(n => n.IsFile).ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
            .Select(ToItem)];
        _treeRevision++; // muda a identidade do MudTreeView → força recriação
    }

    // Indexa a árvore por caminho para o ServerData encontrar os filhos em O(1).
    private void IndexNodes(IEnumerable<Node> nodes)
    {
        foreach (var node in nodes)
        {
            _nodesByPath[node.FullPath] = node;
            IndexNodes(node.Children.Values);
        }
    }

    /// <summary>
    ///     Chamado pelo MudTreeView ao expandir uma pasta: devolve só os filhos
    ///     diretos dela.
    /// </summary>
    private Task<IReadOnlyCollection<TreeItemData<string>>> LoadChildrenAsync(string? path)
    {
        if (path is null || !_nodesByPath.TryGetValue(path, out var node))
            return Task.FromResult<IReadOnlyCollection<TreeItemData<string>>>([]);

        // Pastas primeiro, depois arquivos — a ordem que todo explorador usa.
        IReadOnlyCollection<TreeItemData<string>> children =
        [
            .. node.Children.Values
                .OrderBy(n => n.IsFile)
                .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
                .Select(ToItem)
        ];

        return Task.FromResult(children);
    }

    private async Task OnEditorInit()
    {
        // O Monaco mede o container ao montar. No primeiro render o host ainda
        // não tem altura resolvida, e ele fica preso na mínima. Um Layout()
        // explícito, uma volta de render depois, força-o a remedir os 70vh.
        await Task.Yield();
        await _editor.Layout();

        _editorReady = true;
        _appliedDarkMode = IsDarkMode;
    }

    // O tema do Monaco é global no JS; quando o admin alterna claro/escuro no
    // app, aplicamos aqui para o editor acompanhar sem recarregar a página.
    protected override async Task OnParametersSetAsync()
    {
        if (_editorReady && _appliedDarkMode != IsDarkMode)
        {
            await Global.SetTheme(JsRuntime, MonacoTheme);
            _appliedDarkMode = IsDarkMode;
        }
    }

    private static TreeItemData<string> ToItem(Node node)
    {
        return new TreeItemData<string>
        {
            Text = node.Name,
            Value = node.FullPath,
            Icon = node.IsFile ? Icons.Material.Filled.Description : Icons.Material.Filled.Folder,
            Expandable = !node.IsFile,
            Expanded = false,

            // Children null + Expandable = o MudTreeView pede os filhos ao
            // ServerData quando (e se) a pasta for aberta.
            Children = null
        };
    }

    private async Task OnSelect(string? path)
    {
        _selectedPath = path;
        _notEditable = null;
        if (path is null)
            return;

        // Pastas não abrem no editor — só arquivos de override existentes.
        var isFile = (_version?.Files ?? []).Any(f =>
            f.Origin == ModFileOrigin.Override
            && f.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (!isFile)
            return;

        _isOpening = true;
        try
        {
            var result = await ReadUseCase.HandleAsync(VersionId, path, CancellationToken.None);
            if (!result.Succeeded)
            {
                Snackbar.Add(result.Error!, Severity.Error);
                return;
            }

            // Binário ou grande demais: mostra o cartão com download e nem
            // encosta no Monaco — era isso que travava a aba.
            if (result.Value!.Text is null)
            {
                _notEditable = result.Value;
                return;
            }

            var model = await _editor.GetModel();
            await Global.SetModelLanguage(JsRuntime, model, LanguageFor(path));
            await _editor.SetValue(result.Value.Text);
            _dirty = false;
        }
        finally
        {
            _isOpening = false;
        }
    }


    private async Task Save()
    {
        if (_selectedPath is null)
            return;

        _isSaving = true;
        try
        {
            var content = await _editor.GetValue();
            var result = await SaveUseCase.HandleAsync(VersionId, _selectedPath, content, CancellationToken.None);

            if (result.Succeeded)
            {
                Snackbar.Add("Arquivo salvo.", Severity.Success);
                _dirty = false;
                BuildTree(); // sem recarregar do banco: a árvore muda só se o path for novo
            }
            else
                Snackbar.Add(result.Error!, Severity.Error);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task Delete()
    {
        if (_selectedPath is null)
            return;

        var confirm = await DialogService.ShowMessageBoxAsync(
            "Apagar arquivo", $"Apagar {_selectedPath}?", "Apagar", cancelText: "Cancelar");
        if (confirm is not true)
            return;

        var result = await DeleteUseCase.HandleAsync(VersionId, _selectedPath, CancellationToken.None);
        if (result.Succeeded)
        {
            Snackbar.Add("Arquivo apagado.", Severity.Success);
            _selectedPath = null;
            await _editor.SetValue("");
            await LoadAsync();
        }
        else
            Snackbar.Add(result.Error!, Severity.Error);
    }

    private async Task OpenNewFile()
    {
        var parameters = new DialogParameters { ["VersionId"] = VersionId, ["Folders"] = ExistingFolders() };
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };

        var dialog = await DialogService.ShowAsync<NewOverrideDialog>("Novo arquivo", parameters, options);
        if (await dialog.Result is { Canceled: false, Data: string path })
        {
            await LoadAsync();
            await OnSelect(path); // abre o recém-criado no editor
        }
    }

    // Todas as pastas distintas onde já há overrides (para o seletor do modal).
    private List<string> ExistingFolders()
    {
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in (_version?.Files ?? []).Where(f => f.Origin == ModFileOrigin.Override))
        {
            var slash = file.Path.LastIndexOf('/');
            if (slash <= 0) continue;

            // Inclui a pasta e todas as ancestrais: "config/mod/x.toml" →
            // "config" e "config/mod".
            var dir = file.Path[..slash];
            while (dir.Length > 0)
            {
                folders.Add(dir);
                var up = dir.LastIndexOf('/');
                dir = up < 0 ? "" : dir[..up];
            }
        }

        return folders.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // Realça a linha só se for um alvo válido para o que está a ser arrastado.
    private bool IsDropTarget(TreeItemData<string> item) =>
        _dragPath is not null && _dropTarget == item.Value && item.Value != _dragPath;

    private async Task OnDrop(string? targetPath)
    {
        var from = _dragPath;
        _dragPath = null;
        _dropTarget = null;
        // targetPath vem de ITreeItemData.Value, que é anulável; sem alvo válido
        // não há para onde mover.
        if (from is null || targetPath is null)
            return;

        // Se o alvo é uma pasta, entra nela; se é ficheiro, vai para a pasta dele.
        var isFolder = !(_version?.Files ?? []).Any(f =>
            f.Origin == ModFileOrigin.Override
            && f.Path.Equals(targetPath, StringComparison.OrdinalIgnoreCase));
        var targetFolder = isFolder ? targetPath : ParentOf(targetPath);

        var name = from[(from.LastIndexOf('/') + 1)..];
        var to = string.IsNullOrEmpty(targetFolder) ? name : $"{targetFolder}/{name}";

        if (from == to)
            return;
        if (to.StartsWith(from + "/", StringComparison.OrdinalIgnoreCase))
        {
            Snackbar.Add("Não é possível mover uma pasta para dentro dela mesma.", Severity.Warning);
            return;
        }

        var result = await MoveUseCase.HandleAsync(VersionId, from, to, CancellationToken.None);
        if (result.Succeeded)
        {
            if (_selectedPath == from) _selectedPath = to;
            await LoadAsync();
        }
        else
            Snackbar.Add(result.Error!, Severity.Error);
    }

    private async Task Undo()
    {
        var result = await UndoUseCase.HandleAsync(VersionId, CancellationToken.None);
        if (result.Succeeded)
            await LoadAsync();
        else
            Snackbar.Add(result.Error!, Severity.Info);
    }

    private async Task OnDropRoot()
    {
        var from = _dragPath;
        _dragPath = null;
        _dropTarget = null;
        if (from is null || !from.Contains('/'))
            return; // já está na raiz

        var name = from[(from.LastIndexOf('/') + 1)..];
        var result = await MoveUseCase.HandleAsync(VersionId, from, name, CancellationToken.None);
        if (result.Succeeded)
        {
            if (_selectedPath == from) _selectedPath = name;
            await LoadAsync();
        }
        else
            Snackbar.Add(result.Error!, Severity.Error);
    }

    private static string ParentOf(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash < 0 ? "" : path[..slash];
    }

    private StandaloneEditorConstructionOptions EditorOptions(StandaloneCodeEditor editor)
    {
        return new StandaloneEditorConstructionOptions
        {
            AutomaticLayout = true,
            Theme = MonacoTheme,
            Language = "plaintext",
            Value = "",
            ReadOnly = _version?.State is not ModpackVersionState.Draft
        };
    }

    // Monaco não traz TOML nativo; "ini" dá realce decente para key=value.
    private static string LanguageFor(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".json" or ".json5" => "json",
            ".toml" or ".properties" or ".cfg" or ".conf" or ".ini" => "ini",
            ".yaml" or ".yml" => "yaml",
            ".js" or ".mjs" => "javascript",
            ".xml" => "xml",
            _ => "plaintext"
        };
    }

    private sealed class Node
    {
        public readonly Dictionary<string, Node> Children = new(StringComparer.OrdinalIgnoreCase);
        public string FullPath = "";
        public bool IsFile;
        public string Name = "";
    }
}
