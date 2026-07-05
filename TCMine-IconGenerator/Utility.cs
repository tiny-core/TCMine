using SkiaSharp;
using TCMine_Design;

namespace TCMine_IconGenerator;

public static class Utility
{
    /// <summary>
    /// Localiza a raiz do repositório percorrendo a hierarquia de diretórios a partir do caminho de execução atual
    /// ou de um caminho de substituição opcional. A raiz do repositório é identificada pela presença de um arquivo de solução (*.sln)
    /// ou diretórios de projeto específicos como "TCMine-Launcher" e "TCMine-Server".
    /// </summary>
    /// <param name="overridePath">
    /// Um caminho opcional a ser usado como ponto de partida para encontrar a raiz do repositório.
    /// Se especificado e não for nulo ou espaço em branco, o método retornará diretamente o caminho completo deste override.
    /// </param>
    /// <returns>
    /// O caminho completo para a raiz do repositório.
    /// Lança uma exceção se a raiz não puder ser localizada.
    /// </returns>
    /// <exception cref="DirectoryNotFoundException">
    /// Lançada quando a raiz do repositório não pode ser determinada, indicando que um arquivo de solução
    /// ou diretórios específicos não foram encontrados na hierarquia de diretórios.
    /// </exception>
    public static string FindRepoRoot(string? overridePath)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
            return Path.GetFullPath(overridePath);

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("*.sln").Length != 0
                || dir.GetFiles("*.slnx").Length != 0
                || (Directory.Exists(Path.Combine(dir.FullName, "TCMine-Launcher"))
                    && Directory.Exists(Path.Combine(dir.FullName, "TCMine-Server"))))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Não encontrei a raiz do repositório (.slnx). Passa-a como argumento.");
    }

    /// <summary>
    /// Renderiza um ícone em forma de quadrado arredondado com um design bordado
    /// e um gráfico de cubo incorporado. O ícone renderizado é retornado como uma imagem PNG
    /// na forma de um array de bytes.
    /// </summary>
    /// <param name="s">
    /// O tamanho (em píxeis) da tela quadrada onde o ícone será renderizado.
    /// Este tamanho determina tanto a largura quanto a altura da imagem de saída.
    /// </param>
    /// <returns>
    /// Um byte array contendo a representação PNG do ícone renderizado.
    /// </returns>
    public static byte[] RenderIcon(int s)
    {
        using var bmp = new SKBitmap(s, s, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.Transparent);

        var pad = s * 0.06f;
        var radius = s * 0.22f;
        var tile = new SKRect(pad, pad, s - pad, s - pad);

        using (var fill = new SKPaint())
        {
            fill.Color = SKColor.Parse(ColorTokens.Dark.Background.Page);
            fill.IsAntialias = true;
            canvas.DrawRoundRect(tile, radius, radius, fill);
        }

        using (var border = new SKPaint())
        {
            border.Color = SKColor.Parse(ColorTokens.Primary.Base);
            border.IsAntialias = true;
            border.Style = SKPaintStyle.Stroke;
            border.StrokeWidth = MathF.Max(1f, s * 0.05f);
            var bw = border.StrokeWidth / 2f;
            var inner = new SKRect(tile.Left + bw, tile.Top + bw, tile.Right - bw, tile.Bottom - bw);
            canvas.DrawRoundRect(inner, radius - bw, radius - bw, border);
        }

        DrawCube(canvas, s / 2f, s * 0.52f, s * 0.26f);

        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// Splash do instalador (fundo da marca + cubo + nome)
    public static byte[] RenderSplash(string fontFamily, string title, string desc, int w, int h)
    {
        using var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);

        using (var bg = new SKPaint())
        {
            bg.IsAntialias = true;
            bg.Shader = SKShader.CreateRadialGradient(
                new SKPoint(w / 2f, 0), h * 1.1f,
                [SKColor.Parse(ColorTokens.Dark.Background.Surface), SKColor.Parse(ColorTokens.Dark.Background.Page)],
                [0f, 1f], SKShaderTileMode.Clamp);
            canvas.DrawRect(0, 0, w, h, bg);
        }

        DrawCube(canvas, w / 2f, h * 0.36f, 48f);
        DrawText(fontFamily, canvas, title, w / 2f, h * 0.70f, 27f, SKColor.Parse(ColorTokens.Dark.Text.Primary), true);
        DrawText(fontFamily, canvas, desc, w / 2f, h * 0.70f + 26f, 13f,
            SKColor.Parse(ColorTokens.Dark.Text.Secondary));

        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// Banner social (og-image) do server: fundo da marca + cubo + nome
    public static byte[] RenderBanner(string fontFamily, string title, string desc, int w, int h)
    {
        using var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);

        using (var bg = new SKPaint())
        {
            bg.IsAntialias = true;
            bg.Shader = SKShader.CreateRadialGradient(
                new SKPoint(w / 2f, 0), h * 1.2f,
                [SKColor.Parse(ColorTokens.Dark.Background.Surface), SKColor.Parse(ColorTokens.Dark.Background.Page)],
                [0f, 1f], SKShaderTileMode.Clamp);
            canvas.DrawRect(0, 0, w, h, bg);
        }

        DrawCube(canvas, w / 2f, h * 0.40f, 90f);
        DrawText(fontFamily, canvas, title, w / 2f, h * 0.74f, 56f, SKColor.Parse(ColorTokens.Dark.Text.Primary), true);
        DrawText(fontFamily, canvas, desc, w / 2f, h * 0.74f + 46f, 26f,
            SKColor.Parse(ColorTokens.Dark.Text.Secondary));

        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static void DrawCube(SKCanvas canvas, float cx, float cy, float w)
    {
        var top = new[] { P(cx, cy - w), P(cx + w, cy - w / 2), P(cx, cy), P(cx - w, cy - w / 2) };
        var left = new[] { P(cx - w, cy - w / 2), P(cx, cy), P(cx, cy + w), P(cx - w, cy + w / 2) };
        var right = new[] { P(cx + w, cy - w / 2), P(cx, cy), P(cx, cy + w), P(cx + w, cy + w / 2) };

        // Face de topo: tom claro de destaque (mais próximo do antigo 0xFB923C).
        Face(canvas, top, SKColor.Parse(ColorTokens.Primary.Shade400));
        // Face esquerda: sombra. Não há um shade exato para 0xC2410C no ColorTokens —
        // Shade700 é o mais próximo disponível na escala atual.
        Face(canvas, left, SKColor.Parse(ColorTokens.Primary.Shade700));
        // Face direita: cor de marca, igual ao border do ícone.
        Face(canvas, right, SKColor.Parse(ColorTokens.Primary.Base));
    }

    private static void DrawText(string fontFamily, SKCanvas canvas, string text, float cx, float baseline, float size,
        SKColor color,
        bool bold = false)
    {
        // SkiaSharp 3.x: o tamanho/tipo de letra vivem no SKFont; o alinhamento passa a
        // argumento de DrawText (já não são propriedades do SKPaint, como na 2.x).
        using var typeface = SKTypeface.FromFamilyName(fontFamily,
            bold ? SKFontStyle.Bold : SKFontStyle.Normal);
        using var font = new SKFont(typeface, size);
        font.Edging = SKFontEdging.Antialias;
        using var paint = new SKPaint();
        paint.Color = color;
        paint.IsAntialias = true;
        canvas.DrawText(text, cx, baseline, SKTextAlign.Center, font, paint);
    }

    private static SKPoint P(float x, float y)
    {
        return new SKPoint(x, y);
    }

    private static void Face(SKCanvas canvas, SKPoint[] pts, SKColor color)
    {
        // SkiaSharp 4.x depreciou a construção in-place do SKPath a favor do SKPathBuilder (imutável).
        using var builder = new SKPathBuilder();
        builder.MoveTo(pts[0]);
        for (var i = 1; i < pts.Length; i++) builder.LineTo(pts[i]);
        builder.Close();
        using var path = builder.Snapshot();
        using var paint = new SKPaint();
        paint.Color = color;
        paint.IsAntialias = true;
        paint.Style = SKPaintStyle.Fill;
        canvas.DrawPath(path, paint);
    }

    /// Escreve um .ico com cada tamanho como PNG embutido (suportado no Windows Vista+).
    public static void WriteIco(string path, Dictionary<int, byte[]> images)
    {
        using var fs = File.Create(path);
        using var w = new BinaryWriter(fs);

        var entries = images.OrderBy(kv => kv.Key).ToList();
        w.Write((short)0); // reserved
        w.Write((short)1); // type = icon
        w.Write((short)entries.Count);

        var offset = 6 + entries.Count * 16;
        foreach (var (size, bytes) in entries)
        {
            w.Write((byte)(size >= 256 ? 0 : size)); // width  (0 = 256)
            w.Write((byte)(size >= 256 ? 0 : size)); // height (0 = 256)
            w.Write((byte)0); // palette
            w.Write((byte)0); // reserved
            w.Write((short)1); // color planes
            w.Write((short)32); // bits per pixel
            w.Write(bytes.Length); // size of image data
            w.Write(offset); // offset of image data
            offset += bytes.Length;
        }

        foreach (var (_, bytes) in entries)
            w.Write(bytes);
    }
}