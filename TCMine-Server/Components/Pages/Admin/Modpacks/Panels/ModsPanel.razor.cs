using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using TCMine_Domain.Entities;

namespace TCMine_Server.Components.Pages.Admin.Modpacks;

/// <summary>
/// Aba "Mods" do <see cref="ModpackEditor"/>. Apresentacional: mostra a lista plana de mods
/// (<see cref="Mods"/>, editada por referência) e dispara <see cref="EventCallback"/>s para as ações
/// que precisam da orquestração do editor (buscar/importar/upload/remover). Tem filtro local e paginação.
/// </summary>
public partial class ModsPanel : ComponentBase
{
    [Parameter] [EditorRequired] public List<ModEntryEntity> Mods { get; set; } = null!;
    [Parameter] public bool CfConfigured { get; set; }

    [Parameter] public EventCallback OnSearchCf { get; set; }
    [Parameter] public EventCallback OnImport { get; set; }
    [Parameter] public EventCallback<IBrowserFile> OnUploadJar { get; set; }
    [Parameter] public EventCallback<ModEntryEntity> OnRemove { get; set; }
    [Parameter] public EventCallback OnCheckUpdates { get; set; }

    // Trocar a versão de um mod do CurseForge (busca lazy das versões, só ao clicar)
    [Parameter] public EventCallback<ModEntryEntity> OnChangeVersion { get; set; }

    // Há mods do CurseForge? (só eles têm checagem de atualização)
    private bool HasCurseMods => Mods.Any(m => m.CurseModId > 0);

    private string _filter = string.Empty;

    // Destinos possíveis de um arquivo no cliente (pasta de instalação)
    private static readonly string[] Targets = ["mod", "resourcepack", "shaderpack"];

    // Lista filtrada pelo campo de busca (nome ou arquivo)
    private IEnumerable<ModEntryEntity> Filtered =>
        string.IsNullOrWhiteSpace(_filter)
            ? Mods
            : Mods.Where(m => m.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase)
                              || m.FileName.Contains(_filter, StringComparison.OrdinalIgnoreCase));
}
