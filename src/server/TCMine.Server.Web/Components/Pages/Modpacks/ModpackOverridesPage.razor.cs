using BlazorMonaco.Editor;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Web.Components.Features.Modpacks;

namespace TCMine.Server.Web.Components.Pages.Modpacks;

public partial class ModpackOverridesPage
{
    private List<BreadcrumbItem> _breadcrumbs = [];
    private bool _dirty;
    private StandaloneCodeEditor _editor = default!;
    private bool _isLoading = true;
    private bool _isSaving;
    private string? _selectedPath;
    private List<TreeItemData<string>> _treeItems = [];
    private ModpackVersion? _version;
    [Parameter] public Guid ModpackId { get; set; }
    [Parameter] public Guid VersionId { get; set; }

    [Inject] private IModpackRepository Repository { get; set; } = default!;
    [Inject] private ReadOverride ReadUseCase { get; set; } = default!;
    [Inject] private SaveOverride SaveUseCase { get; set; } = default!;
    [Inject] private DeleteOverride DeleteUseCase { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _version = await Repository.GetVersionAsync(VersionId, CancellationToken.None);

        _breadcrumbs =
        [
            new BreadcrumbItem("Modpacks", "/modpacks"),
            new BreadcrumbItem("Modpack", $"/modpacks/{ModpackId}"),
            new BreadcrumbItem("Overrides", null, true)
        ];

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

        _treeItems = [.. root.Values.Select(ToItem)];
    }

    private async Task OnEditorInit()
    {
        // O Monaco mede o container ao montar. No primeiro render o host ainda
        // não tem altura resolvida, e ele fica preso na mínima. Um Layout()
        // explícito, uma volta de render depois, força-o a remedir os 70vh.
        await Task.Yield();
        await _editor.Layout();
    }

    private static TreeItemData<string> ToItem(Node node)
    {
        return new TreeItemData<string>
        {
            Text = node.Name,
            Value = node.FullPath,
            Icon = node.IsFile ? Icons.Material.Filled.Description : Icons.Material.Filled.Folder,
            Expandable = !node.IsFile,
            Expanded = true,
            Children = node.IsFile ? null : node.Children.Values.Select(ToItem).ToList()
        };
    }

    private async Task OnSelect(string? path)
    {
        _selectedPath = path;
        if (path is null)
            return;

        // Pastas não abrem no editor — só arquivos de override existentes.
        var isFile = (_version?.Files ?? []).Any(f =>
            f.Origin == ModFileOrigin.Override
            && f.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (!isFile)
            return;

        var result = await ReadUseCase.HandleAsync(VersionId, path, CancellationToken.None);
        if (!result.Succeeded)
        {
            Snackbar.Add(result.Error!, Severity.Error);
            return;
        }

        var model = await _editor.GetModel();
        await Global.SetModelLanguage(model, LanguageFor(path));
        await _editor.SetValue(result.Value ?? "");
        _dirty = false;
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
            {
                Snackbar.Add(result.Error!, Severity.Error);
            }
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
        {
            Snackbar.Add(result.Error!, Severity.Error);
        }
    }

    private async Task OpenNewFile()
    {
        var parameters = new DialogParameters
        {
            ["VersionId"] = VersionId,
            ["Folders"] = ExistingFolders()
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };

        var dialog = await DialogService.ShowAsync<NewOverrideDialog>("Novo arquivo", parameters, options);
        if (await dialog.Result is { Canceled: false, Data: string path })
        {
            await LoadAsync();
            await OnSelect(path); // abre o recém-criado no editor
        }
    }

    // Todas as pastas distintas onde já há overrides (para o seletor do modal).
    private IReadOnlyList<string> ExistingFolders()
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

    private StandaloneEditorConstructionOptions EditorOptions(StandaloneCodeEditor editor)
    {
        return new StandaloneEditorConstructionOptions
        {
            AutomaticLayout = true,
            Theme = "vs-dark",
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