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
