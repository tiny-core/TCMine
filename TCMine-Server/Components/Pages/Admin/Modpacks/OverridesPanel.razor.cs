using BlazorMonaco.Editor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using MudBlazor;
using TCMine_Application.Contracts;
using TCMine_Infrastructure.Minecraft;
using TCMine_Server.Components.Pages.Admin.Modpacks.Dialogs;
using TCMine_Server.Services;

namespace TCMine_Server.Components.Pages.Admin.Modpacks;

/// <summary>
/// Painel de edição de overrides de um modpack: árvore de arquivos (<c>MudTreeView</c>) +
/// editor Monaco. Componente próprio (responsabilidade única) — vive separado do editor de
/// metadados. Grava direto no disco via <see cref="ModpackImportService"/> (com histórico/desfazer).
///
/// O editor Monaco fica <b>sempre montado</b>: assim o seu <c>@ref</c> existe quando selecionamos
/// um arquivo. Se a seleção acontecer antes do init do editor, o conteúdo fica pendente e é aplicado
/// no <see cref="OnEditorInitAsync"/>.
/// </summary>
public partial class OverridesPanel : ComponentBase
{
    [Parameter] public Guid ModpackId { get; set; }

    [Inject] private ModpackImportService Service { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;
    [Inject] private BusyService Busy { get; set; } = null!;

    private HashSet<string> _fileSet = [];
    private List<TreeItemData<string>> _treeItems = []; // nível raiz (semente); filhos vêm do ServerData
    private string? _selected;
    private bool _dirty;
    private bool _binary;
    private bool _hasHistory;

    private StandaloneCodeEditor? _editor;
    private bool _editorReady;
    private string? _pendingContent;
    private string? _pendingLang;

    protected override async Task OnInitializedAsync()
    {
        await Busy.RunAsync("Carregando overrides…", () => ReloadAsync(forceTreeRebuild: true));
    }

    // @key do MudTreeView: bumpar reinstancia a árvore. Com lazy loading os filhos já expandidos ficam
    // em cache no componente, então toda mudança estrutural (criar/enviar/apagar/mover/desfazer) reseta
    // a árvore para refletir o disco.
    private int _treeKey;

    // Carregador preguiçoso dos FILHOS (ServerData): chamado ao expandir uma pasta. A raiz é semeada
    // por _treeItems (Items). Mantém _fileSet (arquivos carregados) p/ seleção/edição e detecção
    // arquivo×pasta dos nós visíveis.
    private Task<IReadOnlyCollection<TreeItemData<string>>> LoadTreeAsync(string? parentPath)
    {
        var children = Service.ListOverrideChildren(ModpackId, parentPath ?? string.Empty);
        foreach (var c in children)
            if (!c.IsFolder)
                _fileSet.Add(c.Path);

        IReadOnlyCollection<TreeItemData<string>> items = children.Select(ToTreeItem).ToList();
        return Task.FromResult(items);
    }

    // Converte um item de nível (DTO) num nó do MudTreeView. Pastas ficam expansíveis (ServerData
    // carrega os filhos ao abrir); arquivos não.
    private static TreeItemData<string> ToTreeItem(OverrideNodeDto node)
    {
        return new TreeItemData<string>
        {
            Value = node.Path,
            Text = node.Name,
            Icon = node.IsFolder ? Icons.Material.Filled.Folder : OverrideTreeBuilder.FileIcon(node.Name),
            Expandable = node.IsFolder
        };
    }

    // Recarrega o estado do painel. forceTreeRebuild recarrega a raiz e reseta a árvore (@key).
    private async Task ReloadAsync(bool forceTreeRebuild = false)
    {
        _hasHistory = await Service.GetLastHistoryAsync(ModpackId) is not null;
        if (!forceTreeRebuild) return;

        _fileSet = [];
        var root = Service.ListOverrideChildren(ModpackId, string.Empty);
        foreach (var c in root)
            if (!c.IsFolder)
                _fileSet.Add(c.Path);

        _treeItems = root.Select(ToTreeItem).ToList();
        _treeKey++;
    }

    // ── Seleção / edição ─────────────────────────────────────────────────────────────────────────

    // Clique no nome do nó: arquivo abre no editor; pasta não faz nada (só o chevron expande).
    private async Task OnNodeClick(ITreeItemData<string> node)
    {
        if (node.Expandable) return; // pasta
        _selected = node.Value; // reflete a seleção (highlight) já que não passamos pela seleção do tree
        await OnSelectedChanged(node.Value);
    }

    private async Task OnSelectedChanged(string? value)
    {
        // Clicar numa pasta (ou desselecionar) não abre arquivo — só seleção de arquivo carrega conteúdo
        if (string.IsNullOrEmpty(value) || !_fileSet.Contains(value)) return;

        _selected = value;
        _dirty = false;
        _binary = !ModpackImportService.IsTextOverride(value);

        if (_binary)
        {
            await ApplyToEditorAsync(string.Empty, "plaintext");
            return;
        }

        var content = await Service.ReadOverrideAsync(ModpackId, value) ?? string.Empty;
        await ApplyToEditorAsync(content, LanguageFor(value));
    }

    // Aplica conteúdo+linguagem no editor; se o editor ainda não inicializou, guarda como pendente
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
            await Busy.RunAsync("Salvando override…", async () =>
            {
                await Service.WriteOverrideAsync(ModpackId, _selected, content);
                _dirty = false;
                await ReloadAsync();
            });
            Snackbar.Add("Override salvo.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha ao salvar: {ex.Message}", Severity.Error);
        }
    }

    // ── Criar / enviar / apagar ────────────────────────────────────────────────────────────────

    private async Task NewFileAsync()
    {
        var parameters = new DialogParameters<OverridePathDialog>
        {
            { x => x.Title, "Novo arquivo de override" },
            { x => x.Label, "Caminho (ex.: config/mod.toml)" }
        };
        var dialog = await DialogService.ShowAsync<OverridePathDialog>("Novo override", parameters);
        var result = await dialog.Result;
        if (result is null || result.Canceled || result.Data is not string path || string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            await Busy.RunAsync("Criando override…", async () =>
            {
                await Service.CreateOverrideAsync(ModpackId, path);
                // Abre o novo arquivo no editor (garante no _fileSet, pois a árvore vai resetar p/ raiz)
                var rel = path.Replace('\\', '/');
                _fileSet.Add(rel);
                await OnSelectedChanged(rel);
                await ReloadAsync(forceTreeRebuild: true);
            });
            Snackbar.Add("Override criado.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha ao criar: {ex.Message}", Severity.Error);
        }
    }

    // Move um arquivo ou pasta para outra pasta de destino (escolhida no diálogo; vazio = raiz).
    // Acionado pelo botão à direita de cada item da árvore (alternativa ao drag-and-drop).
    private async Task MoveNodeAsync(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;

        var lastSlash = path.LastIndexOf('/');
        var currentParent = lastSlash >= 0 ? path[..lastSlash] : string.Empty;

        var parameters = new DialogParameters<OverridePathDialog>
        {
            { x => x.Title, "Mover" },
            { x => x.Label, "Pasta de destino (vazio = raiz)" },
            { x => x.Initial, currentParent },
            { x => x.AllowEmpty, true }
        };
        var isFolder = !_fileSet.Contains(path);
        var dialog = await DialogService.ShowAsync<OverridePathDialog>(
            isFolder ? "Mover pasta" : "Mover arquivo", parameters);
        var result = await dialog.Result;
        // Destino vazio é válido (raiz); só aborta em cancelamento
        if (result is null || result.Canceled || result.Data is not string targetFolder) return;

        await MoveToFolderAsync(path, targetFolder);
    }

    // ── Drag-and-drop (mover arrastando) ─────────────────────────────────────────────────────────
    // Payload mantido em memória (mesmo circuito) — não precisa serializar no DataTransfer.

    private string? _dragPath; // item sendo arrastado (o destaque do alvo é feito no JS, client-side)

    private void OnDragStart(string? path)
    {
        _dragPath = path;
    }

    private void OnDragEnd()
    {
        _dragPath = null;
    }

    // Soltou sobre um nó: pasta ⇒ move para dentro dela; arquivo ⇒ move para a pasta-pai dele
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
            targetFolder = nodePath; // é uma pasta
        }

        await MoveToFolderAsync(src, targetFolder);
    }

    // Soltou na área vazia da árvore ⇒ move para a raiz
    private async Task OnDropOnRoot()
    {
        var src = _dragPath;
        OnDragEnd();
        if (string.IsNullOrEmpty(src)) return;
        await MoveToFolderAsync(src, string.Empty);
    }

    // Núcleo do move (compartilhado pelo botão e pelo drag-and-drop). Arquivo vs pasta pelo _fileSet.
    private async Task MoveToFolderAsync(string sourcePath, string targetFolder)
    {
        var isFolder = !_fileSet.Contains(sourcePath);
        try
        {
            await Busy.RunAsync("Movendo…", async () =>
            {
                if (isFolder)
                    await Service.MoveOverrideFolderAsync(ModpackId, sourcePath, targetFolder);
                else
                    await Service.MoveOverrideAsync(ModpackId, sourcePath, targetFolder);

                _selected = null; // o caminho mudou; limpa a seleção
                await ReloadAsync(forceTreeRebuild: true);
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
            await Busy.RunAsync("Enviando override…", async () =>
            {
                // 20 MB por arquivo de override (configs/resourcepacks costumam ser pequenos)
                await using var stream = file.OpenReadStream(20 * 1024 * 1024);
                await Service.UploadOverrideAsync(ModpackId, file.Name, stream);
                await ReloadAsync(forceTreeRebuild: true);
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
            "Apagar override", $"Apagar \"{_selected}\"?", "Apagar", cancelText: "Cancelar");
        if (ok != true) return;

        try
        {
            await Busy.RunAsync("Apagando override…", async () =>
            {
                await Service.DeleteOverrideAsync(ModpackId, _selected);
                _selected = null;
                await ReloadAsync(forceTreeRebuild: true);
            });
            Snackbar.Add("Override apagado.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha ao apagar: {ex.Message}", Severity.Error);
        }
    }

    // ── Histórico / desfazer ─────────────────────────────────────────────────────────────────────

    private async Task UndoLastAsync()
    {
        try
        {
            var undone = await Busy.RunAsync("Desfazendo…", async () =>
            {
                var result = await Service.UndoLastAsync(ModpackId);
                if (result is not null)
                {
                    _selected = null;
                    await ReloadAsync(forceTreeRebuild: true);
                }

                return result;
            });

            if (undone is null)
            {
                Snackbar.Add("Nada para desfazer.", Severity.Info);
                return;
            }

            Snackbar.Add("Última ação desfeita.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha ao desfazer: {ex.Message}", Severity.Error);
        }
    }

    private async Task ShowHistoryAsync()
    {
        var parameters = new DialogParameters<OverrideHistoryDialog> { { x => x.ModpackId, ModpackId } };
        var dialog = await DialogService.ShowAsync<OverrideHistoryDialog>(
            "Histórico de overrides", parameters,
            new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, CloseButton = true });

        var result = await dialog.Result;
        if (result is not null && !result.Canceled)
        {
            _selected = null;
            // O diálogo de histórico pode ter revertido várias ações — reconstrói a árvore
            await Busy.RunAsync("Atualizando overrides…", () => ReloadAsync(forceTreeRebuild: true));
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────

    // Opções de construção do Monaco (tema escuro, layout automático)
    private StandaloneEditorConstructionOptions EditorOptions(StandaloneCodeEditor _)
    {
        return new StandaloneEditorConstructionOptions
        {
            Language = "plaintext",
            Theme = "vs-dark",
            AutomaticLayout = true,
            Value = string.Empty,
            Minimap = new EditorMinimapOptions { Enabled = false },
            FontSize = 13,
            TabSize = 2
        };
    }

    // Mapeia a extensão para a linguagem do Monaco (realce de sintaxe)
    private static string LanguageFor(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
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