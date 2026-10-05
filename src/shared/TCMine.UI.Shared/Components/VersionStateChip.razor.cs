using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Modpacks;

namespace TCMine.UI.Shared.Components;

public partial class VersionStateChip : ComponentBase
{
    [Parameter] [EditorRequired] public ModpackVersionState State { get; set; }

    // Cor, ícone e rótulo do estado num lugar só, como fizemos com o status
    // de servidor. Espalhar esse switch pelas telas leva a "Draft" cinza numa
    // e amarelo em outra.
    private Color Color => State switch
    {
        ModpackVersionState.Draft => Color.Default,
        ModpackVersionState.Resolving => Color.Info,
        ModpackVersionState.Ready => Color.Success,
        ModpackVersionState.Failed => Color.Error,
        ModpackVersionState.Archived => Color.Secondary,
        _ => Color.Default
    };

    private string Icon => State switch
    {
        ModpackVersionState.Draft => Icons.Material.Filled.Edit,
        ModpackVersionState.Resolving => Icons.Material.Filled.Sync,
        ModpackVersionState.Ready => Icons.Material.Filled.CheckCircle,
        ModpackVersionState.Failed => Icons.Material.Filled.ErrorOutline,
        ModpackVersionState.Archived => Icons.Material.Filled.Archive,
        _ => Icons.Material.Filled.HelpOutline
    };

    // Minúsculas: regra do TCMine Design System para chip ("chips em
    // minúsculas", docs/ROADMAP.md).
    private string Label => State switch
    {
        ModpackVersionState.Draft => "rascunho",
        ModpackVersionState.Resolving => "processando",
        ModpackVersionState.Ready => "publicado",
        ModpackVersionState.Failed => "falhou",
        ModpackVersionState.Archived => "arquivado",
        _ => "desconhecido"
    };
}
