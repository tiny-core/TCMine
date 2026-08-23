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

public partial class ModpackOverridesPage : IAsyncDisposable
{
    private bool? _appliedDarkMode;
    private bool _dirty;

    private string? _dragPath; // path a ser arrastado (definido no handle)
    private string? _dropTarget; // path sobre o qual se está a pairar (para realce)
    private StandaloneCodeEditor _editor = default!;

    private bool _editorReady;

    /// <summary>
    ///     Os scripts do Monaco já desceram. Enquanto for falso o editor NÃO
    ///     pode ser renderizado: o BlazorMonaco monta no primeiro render e
    ///     chamaria um JS que ainda não existe.
    /// </summary>
    private bool _monacoLoaded;

    /// <summary>Preenchido quando o editor não pôde ser carregado.</summary>
    private string? _monacoError;

    private IJSObjectReference? _monacoModule;

    /// <summary>
    ///     Completa quando o editor terminou de montar — ou quando desistimos
    ///     dele. Com o Monaco global, o <c>_editor</c> existia desde o primeiro
    ///     render e ninguém precisava esperar; sob demanda existe a janela em
    ///     que o admin clica num arquivo antes de o editor chegar. Sem isto,
    ///     esse clique dava NullReference.
    /// </summary>
    private readonly TaskCompletionSource _editorMounted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
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

    /// <summary>
    ///     Traz os scripts do Monaco só aqui, e não no App.razor.
    ///     Declará-los global fazia TODA página do painel baixar o editor
    ///     inteiro — treze arquivos numa página que não tem editor nenhum.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _monacoLoaded)
            return;

        try
        {
            _monacoModule = await JsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./monaco.js");

            // As URLs saem do Assets porque os estáticos são servidos com hash
            // no nome: escrever o caminho cru no JS pegaria uma versão em cache
            // depois de qualquer atualização.
            // O cast para object é o que faz a lista chegar como UM argumento.
            // InvokeVoidAsync recebe params object[]: sem ele, o array VIRA a
            // lista de argumentos e o JS recebe três strings soltas, ficando com
            // a primeira. Iterar uma string dá os caracteres dela — o editor
            // tentava baixar um script chamado "_" e falhava com uma mensagem
            // que não dizia nada.
            await _monacoModule.InvokeVoidAsync("ensure", (object)new[]
            {
                Assets["_content/BlazorMonaco/jsInterop.js"],
                Assets["_content/BlazorMonaco/lib/monaco-editor/min/vs/loader.js"],
                Assets["_content/BlazorMonaco/lib/monaco-editor/min/vs/editor/editor.main.js"]
            });

            _monacoLoaded = true;
        }
        catch (Exception ex)
        {
            // Sem editor a página ainda serve: a árvore, o mover e o apagar
            // continuam funcionando. Falhar em silêncio é que não pode.
            _monacoError = ex.Message;

            // Libera quem estiver esperando o editor: sem isto um clique feito
            // durante o carregamento ficaria pendurado para sempre.
            _editorMounted.TrySetResult();
        }

        StateHasChanged();
    }

    /// <summary>
    ///     Espera o editor existir. Devolve falso quando ele não vai existir.
    /// </summary>
    private async Task<bool> EditorAvailableAsync()
    {
        await _editorMounted.Task;
        return _monacoError is null;
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
        _editorMounted.TrySetResult();
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

            // Clicar num arquivo enquanto o editor ainda desce é normal na
            // primeira visita à aba; aqui a abertura simplesmente espera, com o
            // _isOpening já mostrando o progresso.
            if (!await EditorAvailableAsync())
                return;

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
            if (!await EditorAvailableAsync())
            {
                Snackbar.Add("O editor não está disponível — recarregue a página.", Severity.Warning);
                return;
            }

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

            // Apagar não depende do editor; só a limpeza da tela depende.
            if (await EditorAvailableAsync())
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

    /// <summary>
    ///     Solta o módulo JS ao sair da página. O circuito do Blazor Server é
    ///     longo: sem isto, cada visita à aba deixaria uma referência viva.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        // A página não tem finalizador; o SuppressFinalize é só o que a CA1816
        // exige de quem implementa o padrão.
        GC.SuppressFinalize(this);

        if (_monacoModule is null)
            return;

        try
        {
            await _monacoModule.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // Circuito já caiu (o admin fechou a aba): não há o que soltar.
        }
    }
}
