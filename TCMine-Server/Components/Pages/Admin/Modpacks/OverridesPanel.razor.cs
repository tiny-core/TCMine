using BlazorMonaco.Editor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using MudBlazor;
using TCMine_Infrastructure.Minecraft;
using TCMine_Server.Components.Pages.Admin.Modpacks.Dialogs;

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

    private List<TreeItemData<string>> _treeItems = [];
    private HashSet<string> _fileSet = [];
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
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        var files = Service.ListOverrideFiles(ModpackId);
        _fileSet = files.Select(f => f.Path).ToHashSet();
        _treeItems = OverrideTreeBuilder.Build(files);
        _hasHistory = await Service.GetLastHistoryAsync(ModpackId) is not null;
    }

    // ── Seleção / edição ─────────────────────────────────────────────────────────────────────────

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
            await Service.WriteOverrideAsync(ModpackId, _selected, content);
            _dirty = false;
            await ReloadAsync();
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
            await Service.CreateOverrideAsync(ModpackId, path);
            await ReloadAsync();
            await OnSelectedChanged(path.Replace('\\', '/'));
            Snackbar.Add("Override criado.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha ao criar: {ex.Message}", Severity.Error);
        }
    }

    private async Task UploadFileAsync(IBrowserFile file)
    {
        try
        {
            // 20 MB por arquivo de override (configs/resourcepacks costumam ser pequenos)
            await using var stream = file.OpenReadStream(20 * 1024 * 1024);
            await Service.UploadOverrideAsync(ModpackId, file.Name, stream);
            await ReloadAsync();
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
            "Apagar override", $"Apagar \"{_selected}\"?", yesText: "Apagar", cancelText: "Cancelar");
        if (ok != true) return;

        try
        {
            await Service.DeleteOverrideAsync(ModpackId, _selected);
            _selected = null;
            await ReloadAsync();
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
            var undone = await Service.UndoLastAsync(ModpackId);
            if (undone is null)
            {
                Snackbar.Add("Nada para desfazer.", Severity.Info);
                return;
            }

            _selected = null;
            await ReloadAsync();
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
            await ReloadAsync();
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
