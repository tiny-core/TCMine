using System.Reflection;
using System.Text;

namespace TCMine.UI.Shared.Theming;

/// <summary>
///     Gera as variáveis CSS a partir dos tokens semânticos.
///     Existe porque algums token não existe na paleta do MudBlazor — os
///     fundos suaves de status (StatusSuccessBg e companhia), por exemplo.
///     Mantê-los num .css escrito à mão criaria duas fontes de verdade que
///     divergem no primeiro ajuste de cor.
///     A varredura pega Light e Dark: como as duas classes declaram os mesmos
///     membros, cada constante vira um par de valores sob o mesmo nome de
///     variável, e o seletor de tema escolhe qual vale.
/// </summary>
public static class TokenCssBuilder
{
    /// <summary>Prefixo das variáveis, para não colidir com as do MudBlazor.</summary>
    private const string Prefix = "--tc";

    public static string Build()
    {
        var claro = ReadTokens(typeof(TcColors.Semantic.Light));
        var escuro = ReadTokens(typeof(TcColors.Semantic.Dark));
        var marca = ReadTokens(typeof(TcColors.Semantic));

        var css = new StringBuilder();

        css.AppendLine("/* Gerado por TokenCssBuilder a partir de TcColors. Não editar. */");

        // :root recebe o tema claro e as cores de marca. É o estado padrão,
        // e vale mesmo se o CSS de tema escuro não carregar.
        css.AppendLine(":root {");
        AppendVariables(css, marca);
        AppendVariables(css, claro);
        css.AppendLine("}");

        // O MudBlazor coloca esta classe no body quando o modo escuro está
        // ativo. O seletor [data-theme] fica como alternativa para o
        // launcher, caso ele não use o layout do Mud.
        css.AppendLine(".mud-theme-dark, [data-theme=\"dark\"] {");
        AppendVariables(css, escuro);
        css.AppendLine("}");

        return css.ToString();
    }

    private static void AppendVariables(StringBuilder css, IEnumerable<KeyValuePair<string, string>> tokens)
    {
        foreach (var (nome, valor) in tokens)
            css.Append("    ").Append(Prefix).Append('-').Append(nome).Append(": ").Append(valor).AppendLine(";");
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
