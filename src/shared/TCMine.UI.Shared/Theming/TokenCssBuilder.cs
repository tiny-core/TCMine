using System.Reflection;
using System.Text;

namespace TCMine.UI.Shared.Theming;

/// <summary>
///     Gera as variáveis CSS a partir dos tokens semânticos.
///     Existe porque algums token não existe na paleta do MudBlazor — os
///     fundos suaves de status (StatusSuccessBg e companhia), por exemplo.
///     Mantê-los num .css escrito à mão criaria duas fontes de verdade que
///     divergem no primeiro ajuste de cor.
///     Um tema só (escuro): as constantes da marca e as do Dark viram
///     variáveis em :root.
/// </summary>
public static class TokenCssBuilder
{
    /// <summary>Prefixo das variáveis, para não colidir com as do MudBlazor.</summary>
    private const string Prefix = "--tc";

    public static string Build()
    {
        var escuro = ReadTokens(typeof(TcColors.Semantic.Dark));
        var marca = ReadTokens(typeof(TcColors.Semantic));

        var css = new StringBuilder();

        css.AppendLine("/* Gerado por TokenCssBuilder a partir de TcColors. Não editar. */");

        // Um tema só, então tudo em :root: vale no painel e no launcher sem
        // depender da classe que o MudBlazor põe no body.
        css.AppendLine(":root {");
        AppendVariables(css, marca);
        AppendVariables(css, escuro);
        css.AppendLine("}");

        return css.ToString();
    }

    private static void AppendVariables(StringBuilder css, IEnumerable<KeyValuePair<string, string>> tokens)
    {
        foreach (var (name, valor) in tokens)
            css.Append("    ").Append(Prefix).Append('-').Append(name).Append(": ").Append(valor).AppendLine(";");
    }

    /// <summary>
    ///     Lê as constantes declaradas DIRETAMENTE no tipo, sem descer às classes
    ///     aninhadas. É o que faz <c>typeof(TcColors.Semantic)</c> devolver só as
    ///     cores de marca, sem arrastar Light e Dark junto — que aqui viriam com
    ///     o mesmo nome de variável e um sobrescreveria o outro.
    /// </summary>
    private static Dictionary<string, string> ReadTokens(Type tipo)
    {
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var campo in tipo.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (!campo.IsLiteral || campo.FieldType != typeof(string))
                continue;

            tokens[ToKebabCase(campo.Name)] = (string)campo.GetRawConstantValue()!;
        }

        return tokens;
    }

    /// <summary>
    ///     StatusSuccessBg vira status-success-bg.
    ///     CSS não distingue maiúsculas de forma confiável entre navegadores, e
    ///     kebab-case é a convenção universal para custom properties.
    /// </summary>
    private static string ToKebabCase(string pascal)
    {
        var sb = new StringBuilder(pascal.Length + 4);

        for (var i = 0; i < pascal.Length; i++)
        {
            var c = pascal[i];

            if (char.IsUpper(c) && i > 0)
                sb.Append('-');

            sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }
}
