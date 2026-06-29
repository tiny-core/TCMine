using System.Text;
using Avalonia.Controls;
using Avalonia.Media;
using TCMine_Design;

namespace TCMine_Launcher.Theme;

/// <summary>
/// Aplica a paleta de <see cref="ColorTokens"/> como recursos Avalonia (Color + SolidColorBrush) no
/// dicionário de recursos da aplicação. Não duplica valores: lê de <see cref="ColorTokens.ToCssVariables"/>,
/// a MESMA fonte usada pelo CSS (admin Blazor) e pelo MudBlazor (TCMine-Server → MudThemeFactory).
/// <para>
/// As chaves geradas são iguais em dark/light — só o valor muda — então dá para chamar
/// <see cref="ApplyTheme"/> de novo ao alternar o tema e os bindings <c>{DynamicResource}</c>
/// atualizam-se sozinhos. Ex.: <c>color-primary-500</c> → <c>Primary500Color</c>/<c>Primary500Brush</c>;
/// <c>color-bg-surface</c> → <c>BgSurfaceColor</c>/<c>BgSurfaceBrush</c>.
/// </para>
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
