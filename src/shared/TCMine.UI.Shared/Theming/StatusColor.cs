using MudBlazor;
using TCMine.Contracts.Servers;

namespace TCMine.UI.Shared.Theming;

/// <summary>
///     Mapeia estados de domínio para cor.
///     Fica fora do MudTheme porque não é paleta e sim tradução de significado
///     para aparência. Espalhar esse switch pelas telas acaba com "Running"
///     verde numa página e azul em outra.
/// </summary>
public static class StatusColors
{
    /// <summary>Cor de destaque, para chip, ícone e texto.</summary>
    public static Color ForServerStatus(GameServerStatus status)
    {
        return status switch
        {
            GameServerStatus.Running => Color.Success,
            GameServerStatus.Starting => Color.Info,
            GameServerStatus.Updating => Color.Info,
            GameServerStatus.Stopping => Color.Warning,
            GameServerStatus.Crashed => Color.Error,
            _ => Color.Default
        };
    }

    /// <summary>
    ///     Fundo suave correspondente, como variável CSS.
    ///     Devolve a variável e não o hexadecimal porque o valor muda com o
    ///     tema: resolver em C# significaria descobrir se o modo escuro está
    ///     ativo e reagir a cada troca. O CSS já faz isso sozinho.
    /// </summary>
    public static string BackgroundVarForServerStatus(GameServerStatus status)
    {
        return status switch
        {
            GameServerStatus.Running => "var(--tc-status-success-bg)",
            GameServerStatus.Starting => "var(--tc-status-info-bg)",
            GameServerStatus.Updating => "var(--tc-status-info-bg)",
            GameServerStatus.Stopping => "var(--tc-status-warning-bg)",
            GameServerStatus.Crashed => "var(--tc-status-error-bg)",
            _ => "transparent"
        };
    }

    /// <summary>Ícone do estado. Cor sozinha exclui quem não a distingue.</summary>
    public static string IconForServerStatus(GameServerStatus status)
    {
        return status switch
        {
            GameServerStatus.Running => Icons.Material.Filled.PlayArrow,
            GameServerStatus.Starting => Icons.Material.Filled.HourglassTop,
            GameServerStatus.Updating => Icons.Material.Filled.Sync,
            GameServerStatus.Stopping => Icons.Material.Filled.HourglassBottom,
            GameServerStatus.Crashed => Icons.Material.Filled.ErrorOutline,
            _ => Icons.Material.Filled.StopCircle
        };
    }
}
