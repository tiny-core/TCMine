using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Web.Components.Shared;

public partial class OriginChip : ComponentBase
{
    [Parameter] [EditorRequired] public ModFileOrigin Origin { get; set; }

    /// <summary>
    ///     Para montar o link à origem (ver <see cref="UpstreamLinks" />).
    ///     Nulo ou sintético (override, upload manual): o selo fica sem link.
    /// </summary>
    [Parameter]
    public string? ProjectSlug { get; set; }

    private string? Page => UpstreamLinks.ProjectPage(Origin, ProjectSlug);

    // Estáticos porque ModpackModsPage também precisa do ícone sozinho, no
    // avatar de cada linha da grade — não só dentro deste selo.
    public static string IconFor(ModFileOrigin origin) => origin switch
    {
        ModFileOrigin.Modrinth or ModFileOrigin.CurseForge => Icons.Material.Filled.Cloud,
        ModFileOrigin.ManualUpload => Icons.Material.Filled.Upload,
        ModFileOrigin.Override => Icons.Material.Filled.Folder,
        _ => Icons.Material.Filled.HelpOutline
    };

    public static Color ColorFor(ModFileOrigin origin) => origin switch
    {
        ModFileOrigin.Modrinth => Color.Success,
        ModFileOrigin.CurseForge => Color.Warning,
        _ => Color.Default
    };

    // Modrinth/CurseForge são marca, ficam como estão; o resto é rótulo
    // nosso — e "ManualUpload" sem isto saía cru, sem espaço, em inglês.
    public static string LabelFor(ModFileOrigin origin) => origin switch
    {
        ModFileOrigin.Modrinth => "Modrinth",
        ModFileOrigin.CurseForge => "CurseForge",
        ModFileOrigin.ManualUpload => "envio manual",
        ModFileOrigin.Override => "override",
        _ => "desconhecida"
    };
}
