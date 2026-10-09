namespace TCMine.UI.Shared.Theming;

public static class TcColors
{
    // --- 1. PALETTE: Onde residem os valores brutos (Primitivos) ---
    //
    // "Pedra vulcânica com luz de minério" — o TCMine Design System. O laranja
    // é LUZ, não tinta: borda acesa, progresso, estado; nunca fundo grande. Não
    // há azul: o papel de "informação" é do laranja-claro.
    public static class Palette
    {
        /// <summary>Superfícies, do afundado ao contorno, e os dois tons de texto.</summary>
        public static class Stone
        {
            public const string Shade1000 = "#0A0A10";
            public const string Shade900 = "#0E0E14";
            public const string Shade800 = "#15151F";
            public const string Shade700 = "#1D1D2A";
            public const string Shade600 = "#2B2B3E";
            public const string Shade500 = "#3A3A52";
            public const string Shade300 = "#8A8AA0";
            public const string Shade100 = "#F2F2F7";
        }

        /// <summary>A luz: primária, clara, brasa (destrutivo) e o núcleo, raríssimo.</summary>
        public static class Ore
        {
            public const string Shade500 = "#FF7A1F";
            public const string Shade300 = "#FFB25C";
            public const string Shade700 = "#E8622C";
            public const string Shade050 = "#FFF3D6";
        }

        public static class Jade
        {
            public const string Shade500 = "#2ED9A0";
        }
    }

    // --- 2. SEMANTIC: Onde residem os, aliás amigáveis (Uso) ---
    public static class Semantic
    {
        // Branding (Agnóstico ao tema)
        public const string BrandPrimary = Palette.Ore.Shade500;
        public const string BrandSecondary = Palette.Ore.Shade300;
        public const string BrandAccent = Palette.Jade.Shade500;

        /// <summary>
        ///     O único tema. O design system é escuro por decisão — a arte é de
        ///     caverna e lava, e um modo claro seria outro produto para manter.
        /// </summary>
        public static class Dark
        {
            public const string BgPage = Palette.Stone.Shade900;
            public const string BgSunken = Palette.Stone.Shade1000;
            public const string BgSurface = Palette.Stone.Shade800;
            public const string SurfaceRaised = Palette.Stone.Shade700;
            public const string SurfaceSelected = "rgba(255,122,31,.10)";
            public const string TextPrimary = Palette.Stone.Shade100;
            public const string TextSecondary = Palette.Stone.Shade300;
            public const string TextMuted = "#5C5C74";
            public const string TextOnPrimary = "#1A1000";
            public const string Border = Palette.Stone.Shade600;
            public const string BorderStrong = Palette.Stone.Shade500;

            // Marca em uso
            public const string PrimaryHover = Palette.Ore.Shade300;
            public const string PrimarySoft = "rgba(255,122,31,.14)";
            public const string PrimaryGlow = "rgba(255,122,31,.35)";

            // Brilho: anel de 1px na cor + halo. Pontual — ação primária, foco, estado ativo.
            public const string GlowPrimary = "0 0 0 1px rgba(255,122,31,.5),0 0 18px rgba(255,122,31,.25)";
            public const string GlowSuccess = "0 0 0 1px rgba(46,217,160,.45),0 0 14px rgba(46,217,160,.22)";
            public const string GlowDanger = "0 0 0 1px rgba(232,98,44,.5),0 0 14px rgba(232,98,44,.22)";
            public const string FocusRing = "0 0 0 3px rgba(255,122,31,.38)";

            // Estados Semânticos
            public const string StatusSuccess = Palette.Jade.Shade500;
            public const string StatusSuccessBg = "rgba(46,217,160,.12)";
            public const string StatusError = Palette.Ore.Shade700;
            public const string StatusErrorBg = "rgba(232,98,44,.14)";
            public const string StatusWarning = Palette.Ore.Shade300;
            public const string StatusWarningBg = "rgba(255,178,92,.12)";
            public const string StatusInfo = Palette.Ore.Shade300;
            public const string StatusInfoBg = "rgba(255,178,92,.12)";
            public const string StatusIdle = Palette.Stone.Shade300;
            public const string StatusIdleBg = "rgba(242,242,247,.06)";
        }
    }
}
