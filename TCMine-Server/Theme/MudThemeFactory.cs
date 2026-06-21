using MudBlazor;
using TCMine_Design;

namespace TCMine_Server.Theme;

/// <summary>
/// Constrói o <see cref="MudThemeFactory"/> do admin Blazor (TCMine) a partir dos tokens
/// centralizados em <see cref="ColorTokens"/> — com <see cref="PaletteDark"/> e
/// <see cref="PaletteLight"/> no mesmo theme, para alternar via <c>IsDarkMode</c>
/// no <c>MudThemeProvider</c> sem trocar de instância de tema.
/// </summary>
/// <remarks>
/// Os nomes das propriedades de ColorTokens correspondem ao MudBlazor v9.5.
/// Se o pacote instalado for uma versão diferente, confirma os nomes das propriedades
/// (algumas foram renomeadas/adicionadas entre major versions).
/// </remarks>
/// <example>
/// <para>
/// MudBlazor — <c>MudThemeFactory.Create()</c> agora devolve um único
/// <c>MudTheme</c> com <c>PaletteDark</c> e <c>PaletteLight</c> preenchidos,
/// no padrão idiomático do MudBlazor:
/// </para>
/// <code>
///     @code {
///         private readonly MudTheme _theme = MudThemeFactory.Create();
///         private bool _isDarkMode = true;
///     }
/// 
/// <![CDATA[
///     <MudThemeProvider Theme="_theme" @bind-IsDarkMode="_isDarkMode" />
///     <MudIconButton Icon="@Icons.Material.Filled.Brightness6"
///                    OnClick="@(() => _isDarkMode = !_isDarkMode)" />
/// ]]>
/// </code>
/// </example>
public static class MudThemeFactory
{
    public static MudTheme Create()
    {
        return new MudTheme
        {
            PaletteDark = BuildPalette<PaletteDark>(true),
            PaletteLight = BuildPalette<PaletteLight>(false),
            LayoutProperties = new LayoutProperties
            {
                DefaultBorderRadius = "8px"
            },
            Typography = new Typography
            {
                Default = new DefaultTypography
                {
                    FontFamily = ["Inter", "Segoe UI", "sans-serif"]
                }
            }
        };
    }

    private static TPalette BuildPalette<TPalette>(bool dark) where TPalette : Palette, new()
    {
        var bgPage = dark ? ColorTokens.Dark.Background.Page : ColorTokens.Light.Background.Page;
        var bgDefault = dark ? ColorTokens.Dark.Background.Default : ColorTokens.Light.Background.Default;
        var bgSurface = dark ? ColorTokens.Dark.Background.Surface : ColorTokens.Light.Background.Surface;
        var border = dark ? ColorTokens.Dark.Background.Border : ColorTokens.Light.Background.Border;
        var borderStrong = dark ? ColorTokens.Dark.Background.BorderStrong : ColorTokens.Light.Background.BorderStrong;

        var textPrimary = dark ? ColorTokens.Dark.Text.Primary : ColorTokens.Light.Text.Primary;
        var textSecondary = dark ? ColorTokens.Dark.Text.Secondary : ColorTokens.Light.Text.Secondary;
        var textDisabled = dark ? ColorTokens.Dark.Text.Disabled : ColorTokens.Light.Text.Disabled;
        var textOnPrimary = dark ? ColorTokens.Dark.Text.OnPrimary : ColorTokens.Light.Text.OnPrimary;

        var success = dark ? ColorTokens.Dark.Semantic.Success : ColorTokens.Light.Semantic.Success;
        var warning = dark ? ColorTokens.Dark.Semantic.Warning : ColorTokens.Light.Semantic.Warning;
        var error = dark ? ColorTokens.Dark.Semantic.Error : ColorTokens.Light.Semantic.Error;
        var info = dark ? ColorTokens.Dark.Semantic.Info : ColorTokens.Light.Semantic.Info;

        return new TPalette
        {
            // Marca
            Primary = ColorTokens.Primary.Base,
            PrimaryContrastText = textOnPrimary,
            PrimaryDarken = ColorTokens.Primary.Active,
            PrimaryLighten = ColorTokens.Primary.Shade300,

            Secondary = ColorTokens.Secondary.Base,
            // Âmbar é claro → texto escuro para contraste legível (não o texto claro padrão).
            SecondaryContrastText = textOnPrimary,
            SecondaryDarken = ColorTokens.Secondary.Active,
            SecondaryLighten = ColorTokens.Secondary.Shade200,

            Tertiary = ColorTokens.Accent.Base,
            // Azul-céu também é claro → texto escuro para contraste.
            TertiaryContrastText = textOnPrimary,
            TertiaryDarken = ColorTokens.Accent.Shade700,
            TertiaryLighten = ColorTokens.Accent.Shade400,

            // Fundos e superfícies
            Black = dark ? bgPage : textPrimary,
            Background = bgDefault,
            BackgroundGray = bgSurface,
            Surface = bgSurface,

            DrawerBackground = bgDefault,
            DrawerText = textSecondary,
            DrawerIcon = textSecondary,

            AppbarBackground = bgSurface,
            AppbarText = textPrimary,

            // Texto
            TextPrimary = textPrimary,
            TextSecondary = textSecondary,
            TextDisabled = textDisabled,

            // Ações, linhas e divisores
            ActionDefault = textSecondary,
            ActionDisabled = textDisabled,
            ActionDisabledBackground =
                dark ? ColorTokens.Dark.Background.Elevated : ColorTokens.Light.Background.Elevated,
            Divider = border,
            DividerLight = border,
            TableLines = border,
            LinesDefault = border,
            LinesInputs = borderStrong,

            // Estados semânticos
            Success = success,
            SuccessContrastText = dark ? bgPage : "#FFFFFF",
            Warning = warning,
            WarningContrastText = dark ? bgPage : "#FFFFFF",
            Error = error,
            ErrorContrastText = dark ? bgPage : "#FFFFFF",
            Info = info,
            InfoContrastText = dark ? bgPage : "#FFFFFF",

            OverlayDark = dark ? "rgba(13,11,9,0.6)" : "rgba(31,27,23,0.5)"
        };
    }
}