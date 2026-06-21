using System.Text;
using Avalonia.Controls;
using Avalonia.Media;

namespace TCMine_Core.Design;

/// <summary>
/// Aplica a paleta de <see cref="ColorTokens"/> como recursos Avalonia (Color + SolidColorBrush)
/// no dicionário de recursos da aplicação do TCMine-Launcher.
/// Não duplica valores: lê diretamente de <see cref="ColorTokens.ToCssVariables"/>, a mesma
/// fonte usada pela versão CSS (admin Blazor) e pelo MudBlazor (<see cref="MudThemeFactory"/>).
/// <para>
/// As chaves de recurso geradas são as MESMAS para dark e light — só o valor muda.
/// Isto permite chamar <see cref="ApplyTheme"/> outra vez ao alternar o tema em runtime,
/// e qualquer binding com <c>{DynamicResource}</c> atualiza-se automaticamente.
/// </para>
/// <example>
/// <para>Como ligar no App.axaml.cs:</para>
/// <code>
///     public override void OnFrameworkInitializationCompleted()
///     {
///         AvaloniaTheme.ApplyTheme(this.Resources, dark: true);
///          
///         // ... Resto do bootstrap (DataTemplates, MainWindow, etc.)
///         base.OnFrameworkInitializationCompleted();
///     }
/// </code>
/// <para>Em um comando de toggle, ex. no ViewModel das settings:</para>
/// <code>
///     private void ToggleTheme(bool dark)
///     {
///         AvaloniaThemeColors.ApplyTheme(Application.Current!.Resources, dark);
///         Application.Current!.RequestedThemeVariant =
///                              dark ? ThemeVariant.Dark : ThemeVariant.Light;
///     }
/// </code>
/// <para>No App.axaml, garante o tema base escuro do FluentTheme antes destes overrides:</para>
/// <code>
/// <![CDATA[
///     <Application.Styles>
///         <FluentTheme />
///         </Application.Styles>
///     <Application RequestedThemeVariant="Dark">
/// ]]>
/// </code>
/// <para>Uso nas views:</para>
/// <code>
/// <![CDATA[
///     <Border Background="{DynamicResource SurfaceColor}"
///             BorderBrush="{DynamicResource BorderColor}"
///             BorderThickness="1"
///             CornerRadius="8">
///         <StackPanel>
///             <TextBlock Text="Servidor ATM10"
///                        Foreground="{DynamicResource TextPrimaryColor}" />
///             <Button Background="{DynamicResource Primary500Brush}"
///                     Foreground="{DynamicResource TextOnPrimaryColor}"
///                     Content="Lançar" />
///         </StackPanel>
///     </Border>
/// ]]>
/// </code>
/// </example>
/// </summary>
public static class AvaloniaTheme
{
    /// <summary>
    /// Gera/atualiza um recurso "{Nome}Color" e "{Nome}Brush" para cada token,
    /// por exemplo, "primary-500" → "Primary500Color" / "Primary500Brush".
    /// </summary>
    /// <param name="resources">Dicionário de recursos da aplicação (ex.: <c>Application.Current.Resources</c>).</param>
    /// <param name="dark">true para o tema escuro, false para o tema claro.</param>
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
    
    /// <summary>"color-primary-500" → "Primary500"</summary>
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
