using System.Text;
using Avalonia.Controls;
using Avalonia.Media;
using TCMine_Design;

namespace TCMine_Launcher.Theme;

/// <summary>
/// Aplica a paleta de <see cref="ColorTokens"/> como recursos Avalonia (Color + SolidColorBrush) no
/// dicionário de recursos da aplicação. Fonte única de cor — mesma de [[entities/tcmine-design]] usada
/// pelo CSS (admin) e MudBlazor.
///
/// Emite dois conjuntos de chaves:
/// <list type="bullet">
///   <item><b>Tokens</b>: <c>Primary500Color</c>/<c>Primary500Brush</c>, <c>BgSurfaceBrush</c>, … (todos
///   os <see cref="ColorTokens.ToCssVariables"/>).</item>
///   <item><b>Aliases semânticos</b> (estilo do backup): <c>BgSidebar</c>, <c>BgConsole</c>,
///   <c>Accent</c>, <c>Danger</c>, … — pensados para os estilos/views herdados do launcher v1, mas
///   com os valores vindos do <see cref="ColorTokens"/> (não hexes literais).</item>
/// </list>
/// As chaves são iguais em dark/light — só o valor muda — então dá para rechamar ao alternar o tema.
/// Os consumidores referenciam via <c>{DynamicResource …}</c> (registado em runtime, antes da janela).
/// </summary>
public static class AvaloniaTheme
{
    public static void ApplyTheme(IResourceDictionary resources, bool dark = true)
    {
        foreach (var (name, hex) in ColorTokens.ToCssVariables(dark))
        {
            var key = ToResourceKey(name);
            var color = Color.Parse(hex);

            resources[key + "Color"] = color;
            resources[key + "Brush"] = new SolidColorBrush(color);
        }

        ApplySemanticAliases(resources, dark);
    }

    /// <summary>
    /// Aliases semânticos do launcher v1 (sidebar/console/acento/estados), mapeados para os valores do
    /// <see cref="ColorTokens"/> — assim os estilos e views herdados funcionam sem hexes próprios.
    /// </summary>
    private static void ApplySemanticAliases(IResourceDictionary r, bool dark)
    {
        // Fundos/bordas/texto dependem do tema; o acento e os estados de marca também têm tom por tema.
        var page = dark ? ColorTokens.Dark.Background.Page : ColorTokens.Light.Background.Page;
        var def = dark ? ColorTokens.Dark.Background.Default : ColorTokens.Light.Background.Default;
        var surface = dark ? ColorTokens.Dark.Background.Surface : ColorTokens.Light.Background.Surface;
        var elevated = dark ? ColorTokens.Dark.Background.Elevated : ColorTokens.Light.Background.Elevated;
        var border = dark ? ColorTokens.Dark.Background.Border : ColorTokens.Light.Background.Border;
        var borderStrong = dark ? ColorTokens.Dark.Background.BorderStrong : ColorTokens.Light.Background.BorderStrong;
        var textPrimary = dark ? ColorTokens.Dark.Text.Primary : ColorTokens.Light.Text.Primary;
        var textSecondary = dark ? ColorTokens.Dark.Text.Secondary : ColorTokens.Light.Text.Secondary;
        var textDisabled = dark ? ColorTokens.Dark.Text.Disabled : ColorTokens.Light.Text.Disabled;
        var onPrimary = dark ? ColorTokens.Dark.Text.OnPrimary : ColorTokens.Light.Text.OnPrimary;
        var success = dark ? ColorTokens.Dark.Semantic.Success : ColorTokens.Light.Semantic.Success;
        var successBg = dark ? ColorTokens.Dark.Semantic.SuccessBg : ColorTokens.Light.Semantic.SuccessBg;
        var warning = dark ? ColorTokens.Dark.Semantic.Warning : ColorTokens.Light.Semantic.Warning;
        var warningBg = dark ? ColorTokens.Dark.Semantic.WarningBg : ColorTokens.Light.Semantic.WarningBg;
        var error = dark ? ColorTokens.Dark.Semantic.Error : ColorTokens.Light.Semantic.Error;
        var errorBg = dark ? ColorTokens.Dark.Semantic.ErrorBg : ColorTokens.Light.Semantic.ErrorBg;

        var map = new Dictionary<string, string>
        {
            // Fundos
            ["BgWindow"] = page,
            ["BgPanel"] = surface,
            ["BgSidebar"] = def,
            ["BgConsole"] = page,
            ["BgInset"] = page,
            ["BgElevated"] = elevated,
            ["BgHover"] = elevated,
            ["BgHoverSoft"] = def,
            // Bordas
            ["BorderChrome"] = border,
            ["BorderSubtle"] = border,
            ["BorderDivider"] = border,
            ["BorderInset"] = border,
            ["BorderHover"] = borderStrong,
            ["BorderMid"] = borderStrong,
            ["BorderStrong"] = borderStrong,
            // Texto
            ["TextPrimary"] = textPrimary,
            ["TextSecondary"] = textSecondary,
            ["TextMuted"] = textSecondary,
            ["TextFaint"] = textDisabled,
            ["TextDim"] = textDisabled,
            ["TextLabel"] = textDisabled,
            ["TextOnPrimary"] = onPrimary,
            // Acento (marca — agnóstico de tema)
            ["Accent"] = ColorTokens.Primary.Shade500,
            ["AccentDark"] = ColorTokens.Primary.Shade600,
            ["AccentDeep"] = ColorTokens.Primary.Shade700,
            ["AccentBg"] = elevated,
            ["AccentBgHover"] = borderStrong,
            // Estados
            ["Success"] = success,
            ["SuccessText"] = success,
            ["SuccessBg"] = successBg,
            ["WarningText"] = warning,
            ["WarningBg"] = warningBg,
            ["Danger"] = error,
            ["DangerText"] = error,
            ["DangerBg"] = errorBg,
            ["DangerBorder"] = error,
            ["DangerSoft"] = error
        };

        foreach (var (key, hex) in map)
            r[key] = new SolidColorBrush(Color.Parse(hex));

        // Cores (não brushes) para gradientes/animações do acento.
        r["AccentColor"] = Color.Parse(ColorTokens.Primary.Shade500);
        r["AccentDarkColor"] = Color.Parse(ColorTokens.Primary.Shade600);
        r["AccentDeepColor"] = Color.Parse(ColorTokens.Primary.Shade700);
    }

    /// <summary>"color-primary-500" → "Primary500".</summary>
    private static string ToResourceKey(string cssVariableName)
    {
        var withoutPrefix = cssVariableName.StartsWith("color-", StringComparison.Ordinal)
            ? cssVariableName["color-".Length..]
            : cssVariableName;

        var sb = new StringBuilder();
        foreach (var part in withoutPrefix.Split('-'))
        {
            if (part.Length == 0) continue;
            sb.Append(char.ToUpperInvariant(part[0])).Append(part[1..]);
        }

        return sb.ToString();
    }
}
