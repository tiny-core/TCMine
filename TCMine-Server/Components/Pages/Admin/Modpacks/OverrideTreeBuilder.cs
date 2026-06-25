using MudBlazor;
using TCMine_Application.Contracts;

namespace TCMine_Server.Components.Pages.Admin.Modpacks;

/// <summary>
/// Converte a lista plana de arquivos de override (caminhos relativos com "/") na árvore
/// hierárquica que o <c>MudTreeView</c> consome. O <c>Value</c> de cada nó é o caminho completo
/// (único): em arquivos é o caminho do arquivo; em pastas, o caminho da pasta.
/// </summary>
public static class OverrideTreeBuilder
{
    public static List<TreeItemData<string>> Build(IEnumerable<OverrideFileDto> files)
    {
        var root = new List<TreeItemData<string>>();
        var nodes = new Dictionary<string, TreeItemData<string>>();
        // Listas de filhos mantidas à parte (a propriedade Children é IReadOnlyCollection — só
        // atribuímos no fim, já ordenada). Chave: caminho da pasta.
        var children = new Dictionary<string, List<TreeItemData<string>>>();

        foreach (var file in files)
        {
            var segments = file.Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var pathSoFar = string.Empty;

            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                var parentPath = pathSoFar;
                pathSoFar = pathSoFar.Length == 0 ? segment : $"{pathSoFar}/{segment}";
                var isFile = i == segments.Length - 1;

                if (nodes.ContainsKey(pathSoFar)) continue; // pasta já criada por outro arquivo

                var node = new TreeItemData<string>
                {
                    Value = pathSoFar,
                    Text = segment,
                    Icon = isFile ? FileIcon(segment) : Icons.Material.Filled.Folder,
                    Expandable = !isFile
                };
                nodes[pathSoFar] = node;
                if (!isFile) children[pathSoFar] = [];

                if (parentPath.Length == 0) root.Add(node);
                else children[parentPath].Add(node);
            }
        }

        // Ordena cada nível (pastas antes de arquivos, alfabético) e amarra os filhos ordenados
        foreach (var (path, list) in children)
        {
            Sort(list);
            nodes[path].Children = list;
        }

        Sort(root);
        return root;
    }

    private static void Sort(List<TreeItemData<string>> list)
    {
        list.Sort((a, b) =>
        {
            // Expandable == pasta; pastas primeiro
            var byKind = b.Expandable.CompareTo(a.Expandable);
            return byKind != 0 ? byKind : string.Compare(a.Text, b.Text, StringComparison.OrdinalIgnoreCase);
        });
    }

    /// <summary>Ícone por extensão para a árvore de overrides.</summary>
    public static string FileIcon(string name)
    {
        var ext = Path.GetExtension(name).ToLowerInvariant();
        return ext switch
        {
            ".json" or ".json5" or ".jsonc" or ".mcmeta" => Icons.Material.Filled.DataObject,
            ".toml" or ".cfg" or ".conf" or ".config" or ".properties" or ".ini" => Icons.Material.Filled.Settings,
            ".png" or ".jpg" or ".jpeg" or ".gif" => Icons.Material.Filled.Image,
            ".zip" or ".jar" => Icons.Material.Filled.Archive,
            _ => Icons.Material.Filled.Description
        };
    }
}