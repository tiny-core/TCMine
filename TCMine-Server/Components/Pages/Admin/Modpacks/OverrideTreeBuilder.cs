using MudBlazor;

namespace TCMine_Server.Components.Pages.Admin.Modpacks;

/// <summary>
///     Helpers da árvore de overrides. A construção é **preguiçosa** (um nível por vez via
///     <c>MudTreeView.ServerData</c> + <c>ModpackImportService.ListOverrideChildren</c>), então aqui
///     fica só o mapeamento de ícone por extensão.
/// </summary>
public static class OverrideTreeBuilder
{
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