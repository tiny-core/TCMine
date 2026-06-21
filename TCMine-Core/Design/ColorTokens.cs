using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace TCMine_Core.Design;

/// <summary>
/// Tokens de cor centralizados do design system TCMine.
/// Cor de marca base: #F97316 (orange-500).
/// <para>
/// As escalas <see cref="Primary"/>, <see cref="Secondary"/> e <see cref="Accent"/> são
/// agnósticas de tema (a identidade da marca não muda entre claro/escuro).
/// <see cref="Dark"/> e <see cref="Light"/> contêm os tokens de fundo, texto e estados
/// semânticos próprios de cada tema, com os MESMOS nomes lógicos nos dois — só o valor muda.
/// Isto permite, por exemplo, no Avalonia, manter as mesmas chaves de recurso e apenas
/// trocar os valores ao alternar o tema em runtime.
/// </para>
/// </summary>
[SuppressMessage("ReSharper", "MemberHidesStaticFromOuterClass")]
public static class ColorTokens
{
    /// <summary>Escala da cor primária (laranja) — marca TCMine. Igual nos dois temas.</summary>
    public static class Primary
    {
        public const string Shade50 = "#FFF4EB";
        public const string Shade100 = "#FFE3CC";
        public const string Shade200 = "#FFC79A";
        public const string Shade300 = "#FDA463";
        public const string Shade400 = "#FB8B3D";
        public const string Shade500 = "#F97316"; // base
        public const string Shade600 = "#DD5E0B";
        public const string Shade700 = "#B84B06";
        public const string Shade800 = "#923B07";
        public const string Shade900 = "#5C2503";
 
        public const string Base = Shade500;
        public const string Hover = Shade600;
        public const string Active = Shade700;
    }
 
    /// <summary>Escala da cor secundária (teal) — ações secundárias e links. Igual nos dois temas.</summary>
    public static class Secondary
    {
        public const string Shade50 = "#E6FBF7";
        public const string Shade100 = "#B8F2E6";
        public const string Shade200 = "#7FE3CE";
        public const string Shade400 = "#3FCBAF";
        public const string Shade500 = "#0D9488"; // base
        public const string Shade600 = "#0A7A70";
        public const string Shade700 = "#08615A";
        public const string Shade900 = "#043330";
 
        public const string Base = Shade500;
        public const string Hover = Shade600;
        public const string Active = Shade700;
    }
 
    /// <summary>Escala da cor de acento (violeta) — destaques pontuais e badges. Igual nos dois temas.</summary>
    public static class Accent
    {
        public const string Shade400 = "#A78BFA";
        public const string Shade500 = "#8B5CF6"; // base
        public const string Shade600 = "#7C3AED";
        public const string Shade700 = "#6D28D9";
 
        public const string Base = Shade500;
        public const string Hover = Shade600;
    }
 
    /// <summary>Tokens próprios do tema escuro: fundos, texto e estados semânticos.</summary>
    public static class Dark
    {
        public static class Background
        {
            public const string Page = "#0D0B09";
            public const string Default = "#161310";
            public const string Surface = "#1F1B17";
            public const string Elevated = "#2A2521";
            public const string Border = "#3D362F";
            public const string BorderStrong = "#56504A";
        }
 
        public static class Text
        {
            public const string Primary = "#F5F1ED";
            public const string Secondary = "#B8AFA6";
            public const string Disabled = "#7A716A";
            public const string OnPrimary = "#2D1500"; // texto escuro sobre Primary.Base
        }
 
        public static class Semantic
        {
            public const string Success = "#34D399";
            public const string SuccessBg = "#0F2E22";
            public const string Warning = "#FBBF24";
            public const string WarningBg = "#332408";
            public const string Error = "#F87171";
            public const string ErrorBg = "#3A1414";
            public const string Info = "#38BDF8";
            public const string InfoBg = "#0D2A38";
        }
    }
 
    /// <summary>Tokens próprios do tema claro: fundos, texto e estados semânticos.</summary>
    public static class Light
    {
        public static class Background
        {
            public const string Page = "#FBF9F7";
            public const string Default = "#FFFFFF";
            public const string Surface = "#FFFFFF";
            public const string Elevated = "#F2EDE7";
            public const string Border = "#E8E1D9";
            public const string BorderStrong = "#D3C9BD";
        }
 
        public static class Text
        {
            public const string Primary = "#1F1B17";
            public const string Secondary = "#6B6259";
            public const string Disabled = "#A89E92";
            public const string OnPrimary = "#2D1500"; // mesma cor: texto escuro sobre Primary.Base
        }
 
        public static class Semantic
        {
            // Tons mais saturados/escuros que no dark mode, para manter contraste AA sobre fundo claro.
            public const string Success = "#15803D";
            public const string SuccessBg = "#ECFDF3";
            public const string Warning = "#B45309";
            public const string WarningBg = "#FFF8EB";
            public const string Error = "#B91C1C";
            public const string ErrorBg = "#FEF1F1";
            public const string Info = "#0369A1";
            public const string InfoBg = "#EFF7FC";
        }
    }
 
    /// <summary>
    /// Converte os tokens num dicionário de variáveis CSS (sem o prefixo "--").
    /// As chaves são as mesmas independentemente de <paramref name="dark"/> — apenas
    /// os valores de fundo/texto/semântico mudam consoante o tema escolhido.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ToCssVariables(bool dark = true)
    {
        return new Dictionary<string, string>
        {
            // Primary
            ["color-primary-50"] = Primary.Shade50,
            ["color-primary-100"] = Primary.Shade100,
            ["color-primary-200"] = Primary.Shade200,
            ["color-primary-300"] = Primary.Shade300,
            ["color-primary-400"] = Primary.Shade400,
            ["color-primary-500"] = Primary.Shade500,
            ["color-primary-600"] = Primary.Shade600,
            ["color-primary-700"] = Primary.Shade700,
            ["color-primary-800"] = Primary.Shade800,
            ["color-primary-900"] = Primary.Shade900,
 
            // Secondary
            ["color-secondary-50"] = Secondary.Shade50,
            ["color-secondary-100"] = Secondary.Shade100,
            ["color-secondary-200"] = Secondary.Shade200,
            ["color-secondary-400"] = Secondary.Shade400,
            ["color-secondary-500"] = Secondary.Shade500,
            ["color-secondary-600"] = Secondary.Shade600,
            ["color-secondary-700"] = Secondary.Shade700,
            ["color-secondary-900"] = Secondary.Shade900,
 
            // Accent
            ["color-accent-400"] = Accent.Shade400,
            ["color-accent-500"] = Accent.Shade500,
            ["color-accent-600"] = Accent.Shade600,
            ["color-accent-700"] = Accent.Shade700,
 
            // Background (depende do tema)
            ["color-bg-page"] = dark ? Dark.Background.Page : Light.Background.Page,
            ["color-bg-default"] = dark ? Dark.Background.Default : Light.Background.Default,
            ["color-bg-surface"] = dark ? Dark.Background.Surface : Light.Background.Surface,
            ["color-bg-elevated"] = dark ? Dark.Background.Elevated : Light.Background.Elevated,
            ["color-border"] = dark ? Dark.Background.Border : Light.Background.Border,
            ["color-border-strong"] = dark ? Dark.Background.BorderStrong : Light.Background.BorderStrong,
 
            // Text (depende do tema)
            ["color-text-primary"] = dark ? Dark.Text.Primary : Light.Text.Primary,
            ["color-text-secondary"] = dark ? Dark.Text.Secondary : Light.Text.Secondary,
            ["color-text-disabled"] = dark ? Dark.Text.Disabled : Light.Text.Disabled,
            ["color-text-on-primary"] = dark ? Dark.Text.OnPrimary : Light.Text.OnPrimary,
 
            // Semantic (depende do tema)
            ["color-success"] = dark ? Dark.Semantic.Success : Light.Semantic.Success,
            ["color-success-bg"] = dark ? Dark.Semantic.SuccessBg : Light.Semantic.SuccessBg,
            ["color-warning"] = dark ? Dark.Semantic.Warning : Light.Semantic.Warning,
            ["color-warning-bg"] = dark ? Dark.Semantic.WarningBg : Light.Semantic.WarningBg,
            ["color-error"] = dark ? Dark.Semantic.Error : Light.Semantic.Error,
            ["color-error-bg"] = dark ? Dark.Semantic.ErrorBg : Light.Semantic.ErrorBg,
            ["color-info"] = dark ? Dark.Semantic.Info : Light.Semantic.Info,
            ["color-info-bg"] = dark ? Dark.Semantic.InfoBg : Light.Semantic.InfoBg,
        };
    }
 
    /// <summary>
    /// Gera o bloco CSS com as variáveis do tema indicado, por exemplo para injetar
    /// em <c>:root[data-theme="dark"]</c> / <c>:root[data-theme="light"]</c> no layout do Blazor Server.
    /// </summary>
    public static string ToCssBlock(bool dark = true, string? selector = null)
    {
        selector ??= dark ? ":root[data-theme=\"dark\"]" : ":root[data-theme=\"light\"]";
 
        var sb = new StringBuilder();
        sb.Append(selector).Append(" {\n");
        foreach (var (name, value) in ToCssVariables(dark))
        {
            sb.Append("  --").Append(name).Append(": ").Append(value).Append(";\n");
        }
        sb.Append('}');
        return sb.ToString();
    }
 
    /// <summary>Gera os dois blocos CSS (dark + light) de uma vez, prontos a injetar no layout.</summary>
    public static string ToCssBlockBoth() => ToCssBlock(dark: true) + "\n\n" + ToCssBlock(dark: false);
}