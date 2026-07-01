using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine_Domain.Entities;
using TCMine_Server.Infrastructure.Server;

namespace TCMine_Server.Components.Pages.Admin.Dashboard.Widgets;

/// <summary>Card da timeline de atividade recente. Apenas apresentação — recebe a lista por parâmetro.</summary>
public partial class RecentActivityCard : ComponentBase
{
    // null = ainda carregando (mostra skeletons); lista vazia = estado "sem atividade"
    [Parameter] public IReadOnlyList<ActivityItem>? Activity { get; set; }

    // Rótulo legível da operação registrada no histórico de overrides
    private static string ActivityLabel(OverrideOp op)
    {
        return op switch
        {
            OverrideOp.Edit => "Arquivo editado",
            OverrideOp.MoveFile => "Arquivo movido",
            OverrideOp.MoveFolder => "Pasta movida",
            OverrideOp.DeleteFile => "Arquivo excluído",
            _ => op.ToString()
        };
    }

    // Ícone que representa a operação na timeline
    private static string ActivityIcon(OverrideOp op)
    {
        return op switch
        {
            OverrideOp.Edit => Icons.Material.Filled.Edit,
            OverrideOp.MoveFile => Icons.Material.Filled.DriveFileMove,
            OverrideOp.MoveFolder => Icons.Material.Filled.FolderOpen,
            OverrideOp.DeleteFile => Icons.Material.Filled.Delete,
            _ => Icons.Material.Filled.History
        };
    }

    // Exclusão em vermelho, o resto no acento do tema
    private static Color ActivityColor(OverrideOp op)
    {
        return op == OverrideOp.DeleteFile ? Color.Error : Color.Primary;
    }

    // Nome do modpack; cai para o id quando o modpack já foi excluído (nome indisponível)
    private static string ActivityModpack(ActivityItem a)
    {
        return string.IsNullOrWhiteSpace(a.ModpackName) ? a.ModpackId.ToString() : a.ModpackName;
    }

    // Caminho mais relevante (destino quando há, senão a origem)
    private static string ActivityPath(ActivityItem a)
    {
        return a.PathAfter ?? a.PathBefore ?? "—";
    }
}