using MudBlazor;

namespace TCMine.UI.Shared.Theming;

/// <summary>
///     Ponte entre os tokens do TCMine e a paleta do MudBlazor.
///     Nenhum valor hexadecimal aparece aqui — tudo referencia TcColors. Assim
///     existe uma fonte de verdade só: mudar o laranja da marca em TcColors
///     reflete no painel e no launcher sem tocar neste arquivo.
/// </summary>
public static class TcMineTheme
{
    public static MudTheme Default { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = TcColors.Semantic.BrandPrimary,
            Secondary = TcColors.Semantic.BrandSecondary,
            Tertiary = TcColors.Semantic.BrandAccent,
            Success = TcColors.Semantic.Light.StatusSuccess,
            Error = TcColors.Semantic.Light.StatusError,
            Warning = TcColors.Semantic.Light.StatusWarning,
            Info = TcColors.Semantic.Light.StatusInfo,
            Background = TcColors.Semantic.Light.BgPage,
            Surface = TcColors.Semantic.Light.BgSurface,
            DrawerBackground = TcColors.Semantic.Light.BgSurface,
            AppbarBackground = TcColors.Semantic.Light.BgSurface,
            TextPrimary = TcColors.Semantic.Light.TextPrimary,
            TextSecondary = TcColors.Semantic.Light.TextSecondary,
            TextDisabled = TcColors.Semantic.Light.TextMuted,

            // No tema claro a appbar usa fundo de superfície, então o texto
            // dela precisa ser escuro — o padrão do MudBlazor é branco.
            AppbarText = TcColors.Semantic.Light.TextPrimary,
            DrawerText = TcColors.Semantic.Light.TextPrimary,
            DrawerIcon = TcColors.Semantic.Light.TextSecondary,
            Divider = TcColors.Semantic.Light.Border,
            LinesDefault = TcColors.Semantic.Light.Border,
            TableLines = TcColors.Semantic.Light.Border,
            ActionDefault = TcColors.Semantic.Light.TextSecondary,
            ActionDisabled = TcColors.Semantic.Light.TextMuted
        },
        PaletteDark = new PaletteDark
        {
            Primary = TcColors.Semantic.BrandPrimary,
            Secondary = TcColors.Semantic.BrandSecondary,
            Tertiary = TcColors.Semantic.BrandAccent,
            Success = TcColors.Semantic.Dark.StatusSuccess,
            Error = TcColors.Semantic.Dark.StatusError,
            Warning = TcColors.Semantic.Dark.StatusWarning,
            Info = TcColors.Semantic.Dark.StatusInfo,
            Background = TcColors.Semantic.Dark.BgPage,
            Surface = TcColors.Semantic.Dark.BgSurface,

            // Um degrau mais escuro que a superfície, para dar profundidade
            // sem precisar de sombra.
            DrawerBackground = TcColors.Palette.Neutral.Slate800,
            AppbarBackground = TcColors.Palette.Neutral.Slate800,
            TextPrimary = TcColors.Semantic.Dark.TextPrimary,
            TextSecondary = TcColors.Semantic.Dark.TextSecondary,
            TextDisabled = TcColors.Semantic.Dark.TextMuted,
            AppbarText = TcColors.Semantic.Dark.TextPrimary,
            DrawerText = TcColors.Semantic.Dark.TextPrimary,
            DrawerIcon = TcColors.Semantic.Dark.TextSecondary,
            Divider = TcColors.Semantic.Dark.Border,
            LinesDefault = TcColors.Semantic.Dark.Border,
            TableLines = TcColors.Semantic.Dark.Border,
            ActionDefault = TcColors.Semantic.Dark.TextSecondary,
            ActionDisabled = TcColors.Semantic.Dark.TextMuted
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "8px", DrawerWidthLeft = "260px" },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = ["Inter", "Segoe UI", "Roboto", "sans-serif"] }
        }
    };
}
