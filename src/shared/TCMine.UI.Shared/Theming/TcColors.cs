using System.Reflection;

namespace TCMine.UI.Shared.Theming;

public class TcColors
{
    /// <summary>
    ///     Varre recursivamente todas as classes internas e retorna um dicionário com chave/valor de todas as cores.
    /// </summary>
    public static Dictionary<string, string> GetAllTokens()
    {
        var tokens = new Dictionary<string, string>();
        ExtractTokensRecursive(typeof(TcColors), "", tokens);
        return tokens;
    }

    private static void ExtractTokensRecursive(Type type, string prefix, Dictionary<string, string> dictionary)
    {
        // Pega todas as constantes (const string) da classe atual
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);
        foreach (var field in fields)
        {
            if (field.FieldType != typeof(string) || !field.IsLiteral) continue;
            var value = (string)field.GetValue(null)!;
            // Concatena o prefixo hierárquico (ex: Semantic + Dark + BgPage = SemanticDarkBgPage)
            var key = prefix + field.Name;
            dictionary[key] = value;
        }

        // Percorre as classes aninhadas (Palette, Semantic, Orange, Dark, Light, etc.)
        var nestedTypes = type.GetNestedTypes(BindingFlags.Public);
        foreach (var nestedType in nestedTypes)
            ExtractTokensRecursive(nestedType, prefix + nestedType.Name, dictionary);
    }

    // --- 1. PALETTE: Onde residem os valores brutos (Primitivos) ---
    public static class Palette
    {
        public static class Orange
        {
            public const string Shade50 = "#FFF7ED";
            public const string Shade100 = "#FFEDD5";
            public const string Shade200 = "#FED7AA";
            public const string Shade300 = "#FDBA74";
            public const string Shade400 = "#FB923C";
            public const string Shade500 = "#F97316";
            public const string Shade600 = "#EA580C";
            public const string Shade700 = "#C2410C";
            public const string Shade800 = "#9A3412";
            public const string Shade900 = "#7C2D12";
        }

        public static class Amber
        {
            public const string Shade50 = "#FFFBEB";
            public const string Shade100 = "#FEF3C7";
            public const string Shade200 = "#FDE68A";
            public const string Shade400 = "#FBBF24";
            public const string Shade500 = "#F59E0B";
            public const string Shade600 = "#D97706";
            public const string Shade700 = "#B45309";
            public const string Shade900 = "#78350F";
        }

        public static class Sky
        {
            public const string Shade400 = "#7DD3FC";
            public const string Shade500 = "#38BDF8";
            public const string Shade600 = "#0EA5E9";
            public const string Shade700 = "#0284C7";
        }

        public static class Neutral
        {
            public const string Slate900 = "#0B0B14";
            public const string Slate800 = "#0F0F1A";
            public const string Slate700 = "#14141F";
            public const string Slate600 = "#1B1B2A";
            public const string Slate500 = "#242438";
            public const string Slate400 = "#34344E";
            public const string Slate300 = "#94A3B8";
            public const string Slate200 = "#E8E8F0";
            public const string Slate100 = "#6A6A8A";
        }
    }

    // --- 2. SEMANTIC: Onde residem os, aliás amigáveis (Uso) ---
    public static class Semantic
    {
        // Branding (Agnóstico ao tema)
        public const string BrandPrimary = Palette.Orange.Shade500;
        public const string BrandSecondary = Palette.Amber.Shade500;
        public const string BrandAccent = Palette.Sky.Shade500;

        // Tema Escuro
        public static class Dark
        {
            public const string BgPage = Palette.Neutral.Slate900;
            public const string BgSurface = Palette.Neutral.Slate700;
            public const string TextPrimary = Palette.Neutral.Slate200;
            public const string TextSecondary = Palette.Neutral.Slate300;
            public const string TextMuted = Palette.Neutral.Slate100;
            public const string Border = Palette.Neutral.Slate500;

            // Estados Semânticos
            public const string StatusSuccess = "#34D399";
            public const string StatusSuccessBg = "#0F2E22";
            public const string StatusError = "#F87171";
            public const string StatusErrorBg = "#3A1414";
            public const string StatusWarning = "#FBBF24";
            public const string StatusWarningBg = "#332408";
            public const string StatusInfo = "#38BDF8";
            public const string StatusInfoBg = "#0D2A38";
        }

        // Tema Claro
        public static class Light
        {
            public const string BgPage = "#FBF9F7";
            public const string BgSurface = "#FFFFFF";
            public const string TextPrimary = "#1F1B17";
            public const string TextSecondary = "#6B6259";
            public const string TextMuted = "#A89E92";
            public const string Border = "#E8E1D9";

            // Estados Semânticos
            public const string StatusSuccess = "#15803D";
            public const string StatusSuccessBg = "#ECFDF3";
            public const string StatusError = "#B91C1C";
            public const string StatusErrorBg = "#FEF1F1";
            public const string StatusWarning = "#B45309";
            public const string StatusWarningBg = "#FFF8EB";
            public const string StatusInfo = "#0369A1";
            public const string StatusInfoBg = "#EFF7FC";
        }
    }
}