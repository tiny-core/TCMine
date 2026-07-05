using BlazorMonaco.Editor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using MudBlazor;
using TCMine_Server.Components.Pages.Admin.Modpacks.Dialogs;
using TCMine_Server.Services;

namespace TCMine_Server.Components.Shared;

/// <summary>
///     Componente compartilhado de árvore de arquivos + editor Monaco. Dirige-se por um
///     <see cref="IFileTreeSource" /> (overrides de modpack ou configs de servidor). O Monaco fica sempre
///     montado; se a seleção acontecer antes do init, o conteúdo fica pendente e é aplicado no init. As
///     operações (criar/enviar/apagar/mover) e o histórico/desfazer específicos do host ficam fora —
///     expostos via capacidades do source e o slot <see cref="ToolbarExtra" />.
/// </summary>
public partial class FileTreeEditor : ComponentBase
{
    private bool _binary;
    private bool _dirty;
    private string? _dragPath; // item arrastado (destaque do alvo é client-side via overrides-dnd.js)

    private StandaloneCodeEditor? _editor;
    private bool _editorReady;

    private HashSet<string> _fileSet = [];
    private string? _pendingContent;
    private string? _pendingLang;
    private string? _selected;
    private List<TreeItemData<string>> _treeItems = [];
    private int _treeKey; // bumpar reinstancia a árvore (lazy) para refletir o disco
    [Parameter] [EditorRequired] public IFileTreeSource Source { get; set; } = null!;

    /// <summary>Botões extras no toolbar (ex.: histórico/desfazer dos overrides).</summary>
    [Parameter]
    public RenderFragment? ToolbarExtra { get; set; }

    /// <summary>Disparado após mudanças estruturais/salvar — o host atualiza o seu estado (ex.: histórico).</summary>
    [Parameter]
    public EventCallback OnChanged { get; set; }

    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;
    [Inject] private BusyService Busy { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        await Busy.RunAsync(Source.LoadingMessage, () => ReloadAsync(true));
    }

    /// <summary>Reconstrói a árvore do zero (uso do host após operações externas, ex.: desfazer).</summary>
    public Task RefreshAsync()
    {
        return ReloadAsync(true);
    }

    // ── Árvore ───────────────────────────────────────────────────────────────────────────────────

    private Task<IReadOnlyCollection<TreeItemData<string>>> LoadTreeAsync(string? parentPath)
    {
        var children = Source.ListChildren(parentPath ?? string.Empty);
        foreach (var c in children)
            if (!c.IsFolder)
                _fileSet.Add(c.Path);

        IReadOnlyCollection<TreeItemData<string>> items = children.Select(ToTreeItem).ToList();
        return Task.FromResult(items);
    }

    private TreeItemData<string> ToTreeItem(FileTreeNode node)
    {
        return new TreeItemData<string>
        {
            Value = node.Path,
            Text = node.Name,
            Icon = node.IsFolder ? Icons.Material.Filled.Folder : Source.FileIcon(node.Name),
            Expandable = node.IsFolder
        };
    }

    private async Task ReloadAsync(bool forceTreeRebuild = false)
    {
        if (forceTreeRebuild)
        {
            _fileSet = [];
            var root = Source.ListChildren(string.Empty);
            foreach (var c in root)
                if (!c.IsFolder)
                    _fileSet.Add(c.Path);

            _treeItems = root.Select(ToTreeItem).ToList();
            _treeKey++;
        }

        if (OnChanged.HasDelegate) await OnChanged.InvokeAsync();
    }

    // ── Seleção / edição ─────────────────────────────────────────────────────────────────────────

    private async Task OnNodeClick(ITreeItemData<string> node)
    {
        if (node.Expandable) return; // pasta
        _selected = node.Value;
        await OnSelectedChanged(node.Value);
    }

    private async Task OnSelectedChanged(string? value)
    {
        if (string.IsNullOrEmpty(value) || !_fileSet.Contains(value)) return;

        _selected = value;
        _dirty = false;
        _binary = !Source.IsText(value);

        if (_binary)
        {
            await ApplyToEditorAsync(string.Empty, "plaintext");
            return;
        }

        var content = await Source.ReadAsync(value) ?? string.Empty;
        await ApplyToEditorAsync(content, LanguageFor(value));
    }

    private async Task ApplyToEditorAsync(string content, string language)
    {
        if (_editorReady && _editor is not null)
        {
            await _editor.SetValue(content);
            var model = await _editor.GetModel();
            if (model is not null) await Global.SetModelLanguage(JsRuntime, model, language);
        }
        else
        {
            _pendingContent = content;
            _pendingLang = language;
        }
    }

    private async Task OnEditorInitAsync()
    {
        _editorReady = true;
        if (_pendingContent is null) return;

        await _editor!.SetValue(_pendingContent);
        var model = await _editor.GetModel();
        if (model is not null) await Global.SetModelLanguage(JsRuntime, model, _pendingLang ?? "plaintext");
        _pendingContent = null;
        _pendingLang = null;
    }

    private void OnContentChanged()
    {
        _dirty = true;
    }

    private async Task SaveFileAsync()
    {
        if (_selected is null || _editor is null || _binary) return;

        try
        {
            var content = await _editor.GetValue();
            await Busy.RunAsync("Salvando arquivo…", async () =>
            {
                await Source.WriteAsync(_selected, content);
                _dirty = false;
                await ReloadAsync();
            });
            Snackbar.Add("Arquivo salvo.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha ao salvar: {ex.Message}", Severity.Error);
        }
    }

    // ── Criar / enviar / apagar / mover ────────────────────────────────────────────────────────────

    private async Task NewFileAsync()
    {
        var parameters = new DialogParameters<OverridePathDialog>
        {
            { x => x.Title, "Novo arquivo" },
            { x => x.Label, "Caminho (ex.: config/mod.toml)" }
        };
        var dialog = await DialogService.ShowAsync<OverridePathDialog>("Novo arquivo", parameters);
        var result = await dialog.Result;
        if (result is null || result.Canceled || result.Data is not string path || string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            await Busy.RunAsync("Criando arquivo…", async () =>
            {
                await Source.CreateAsync(path);
                var rel = path.Replace('\\', '/');
                _fileSet.Add(rel);
                await ReloadAsync(true);
                await OnSelectedChanged(rel);
            });
            Snackbar.Add("Arquivo criado.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha ao criar: {ex.Message}", Severity.Error);
        }
    }

    private async Task MoveNodeAsync(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;

        var lastSlash = path.LastIndexOf('/');
        var currentParent = lastSlash >= 0 ? path[..lastSlash] : string.Empty;
        var isFolder = !_fileSet.Contains(path);

        var parameters = new DialogParameters<OverridePathDialog>
        {
            { x => x.Title, "Mover" },
            { x => x.Label, "Pasta de destino (vazio = raiz)" },
            { x => x.Initial, currentParent },
            { x => x.AllowEmpty, true }
        };
        var dialog = await DialogService.ShowAsync<OverridePathDialog>(
            isFolder ? "Mover pasta" : "Mover arquivo", parameters);
        var result = await dialog.Result;
        if (result is null || result.Canceled || result.Data is not string targetFolder) return;

        await MoveToFolderAsync(path, targetFolder);
    }

    private void OnDragStart(string? path)
    {
        _dragPath = path;
    }

    private void OnDragEnd()
    {
        _dragPath = null;
    }

    private async Task OnDropOnNode(string? nodePath)
    {
        var src = _dragPath;
        OnDragEnd();
        if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(nodePath) || src == nodePath) return;

        string targetFolder;
        if (_fileSet.Contains(nodePath))
        {
            var slash = nodePath.LastIndexOf('/');
            targetFolder = slash >= 0 ? nodePath[..slash] : string.Empty;
        }
        else
        {
            targetFolder = nodePath; // pasta
        }

        await MoveToFolderAsync(src, targetFolder);
    }

    private async Task OnDropOnRoot()
    {
        var src = _dragPath;
        OnDragEnd();
        if (string.IsNullOrEmpty(src)) return;
        await MoveToFolderAsync(src, string.Empty);
    }

    private async Task MoveToFolderAsync(string sourcePath, string targetFolder)
    {
        var isFolder = !_fileSet.Contains(sourcePath);
        try
        {
            await Busy.RunAsync("Movendo…", async () =>
            {
                await Source.MoveAsync(sourcePath, targetFolder, isFolder);
                _selected = null;
                await ReloadAsync(true);
            });
            Snackbar.Add(isFolder ? "Pasta movida." : "Arquivo movido.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha ao mover: {ex.Message}", Severity.Error);
        }
    }

    private async Task UploadFileAsync(IBrowserFile file)
    {
        try
        {
            await Busy.RunAsync("Enviando arquivo…", async () =>
            {
                await using var stream = file.OpenReadStream(20 * 1024 * 1024);
                await Source.UploadAsync(file.Name, stream);
                await ReloadAsync(true);
            });
            Snackbar.Add($"\"{file.Name}\" enviado.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha no upload: {ex.Message}", Severity.Error);
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (_selected is null) return;

        var ok = await DialogService.ShowMessageBoxAsync(
            "Apagar arquivo", $"Apagar \"{_selected}\"?", "Apagar", cancelText: "Cancelar");
        if (ok != true) return;

        try
        {
            await Busy.RunAsync("Apagando arquivo…", async () =>
            {
                await Source.DeleteFileAsync(_selected);
                _selected = null;
                await ReloadAsync(true);
            });
            Snackbar.Add("Arquivo apagado.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha ao apagar: {ex.Message}", Severity.Error);
        }
    }

    // ── Helpers Monaco ─────────────────────────────────────────────────────────────────────────────

    private static StandaloneEditorConstructionOptions EditorOptions(StandaloneCodeEditor _)
    {
        return new StandaloneEditorConstructionOptions
        {
            Language = "plaintext", Theme = "vs-dark", AutomaticLayout = true, Value = string.Empty,
            Minimap = new EditorMinimapOptions { Enabled = false }, FontSize = 13, TabSize = 2
        };
    }

    private static string LanguageFor(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".json" or ".json5" or ".jsonc" or ".mcmeta" => "json",
            ".toml" => "toml",
            ".yaml" or ".yml" => "yaml",
            ".xml" => "xml",
            ".js" or ".zs" => "javascript",
            ".properties" or ".cfg" or ".conf" or ".config" or ".ini" or ".lang" => "ini",
            ".md" => "markdown",
            _ => "plaintext"
        };
    }
}