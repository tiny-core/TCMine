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
    /// <summary>
    ///     As duas paletas recebem os MESMOS valores: o TCMine só tem tema escuro
    ///     (ver <see cref="TcColors.Semantic.Dark" />), e um componente que por
    ///     engano renderizasse no modo claro do MudBlazor mostraria o branco
    ///     padrão dele no meio da pedra.
    /// </summary>
    public static MudTheme Default { get; } = new()
    {
        PaletteLight = Apply(new PaletteLight()),
        PaletteDark = Apply(new PaletteDark()),
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "8px", DrawerWidthLeft = "260px" },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = ["Inter", "Segoe UI", "Roboto", "sans-serif"] },

            // Sentence case: "Novo modpack", nunca "NOVO MODPACK". Caixa alta
            // fica só no rótulo micro (cabeçalho de tabela, grupo do menu).
            Button = new ButtonTypography { TextTransform = "none", FontWeight = "600" },
            H4 = new H4Typography { FontWeight = "700" }
        }
    };

    private static T Apply<T>(T palette) where T : Palette
    {
        palette.Primary = TcColors.Semantic.BrandPrimary;
        palette.PrimaryContrastText = TcColors.Semantic.Dark.TextOnPrimary;
        palette.Secondary = TcColors.Semantic.BrandSecondary;
        palette.SecondaryContrastText = TcColors.Semantic.Dark.TextOnPrimary;
        palette.Tertiary = TcColors.Semantic.BrandAccent;
        palette.Success = TcColors.Semantic.Dark.StatusSuccess;
        palette.Error = TcColors.Semantic.Dark.StatusError;
        palette.Warning = TcColors.Semantic.Dark.StatusWarning;
        palette.Info = TcColors.Semantic.Dark.StatusInfo;
        palette.Black = TcColors.Palette.Stone.Shade1000;
        palette.Background = TcColors.Semantic.Dark.BgPage;
        palette.BackgroundGray = TcColors.Semantic.Dark.BgSunken;
        palette.Surface = TcColors.Semantic.Dark.BgSurface;

        // Um degrau mais escuro que a página, para a moldura recuar e o
        // conteúdo vir à frente sem precisar de sombra.
        palette.DrawerBackground = TcColors.Semantic.Dark.BgSunken;
        palette.AppbarBackground = TcColors.Semantic.Dark.BgPage;
        palette.TextPrimary = TcColors.Semantic.Dark.TextPrimary;
        palette.TextSecondary = TcColors.Semantic.Dark.TextSecondary;
        palette.TextDisabled = TcColors.Semantic.Dark.TextMuted;
        palette.AppbarText = TcColors.Semantic.Dark.TextPrimary;
        palette.DrawerText = TcColors.Semantic.Dark.TextSecondary;
        palette.DrawerIcon = TcColors.Semantic.Dark.TextSecondary;
        palette.Divider = TcColors.Semantic.Dark.Border;
        palette.DividerLight = TcColors.Semantic.Dark.Border;
        palette.LinesDefault = TcColors.Semantic.Dark.Border;
        palette.LinesInputs = TcColors.Semantic.Dark.BorderStrong;
        palette.TableLines = TcColors.Semantic.Dark.Border;
        palette.TableHover = TcColors.Semantic.Dark.SurfaceRaised;
        palette.ActionDefault = TcColors.Semantic.Dark.TextSecondary;
        palette.ActionDisabled = TcColors.Semantic.Dark.TextMuted;
        palette.ActionDisabledBackground = TcColors.Semantic.Dark.SurfaceRaised;
        palette.OverlayDark = "rgba(10,10,16,.72)";
        return palette;
    }
}
