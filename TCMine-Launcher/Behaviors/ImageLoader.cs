using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using TCMine_Launcher.Infrastructure.FileSystem;
using TCMine_Launcher.Infrastructure.Networking;

namespace TCMine_Launcher.Behaviors;

/// <summary>
///     Propriedade anexada que carrega uma imagem de um URL (cache em memória + disco) e a põe no
///     <see cref="Image.Source" />. Uso: <c>&lt;Image bh:ImageLoader.SourceUrl="{Binding HeadUrl}" /&gt;</c>.
///     Falhas de rede são ignoradas (fica sem imagem). Camada de View.
/// </summary>
public static class ImageLoader
{
    private static readonly ConcurrentDictionary<string, Bitmap> Cache = new();

    public static readonly AttachedProperty<string?> SourceUrlProperty =
        AvaloniaProperty.RegisterAttached<Image, string?>("SourceUrl", typeof(ImageLoader));

    static ImageLoader()
    {
        SourceUrlProperty.Changed.AddClassHandler<Image>((image, e) => _ = LoadAsync(image, e.NewValue as string));
    }

    public static void SetSourceUrl(Image element, string? value)
    {
        element.SetValue(SourceUrlProperty, value);
    }

    private static string? GetSourceUrl(Image element)
    {
        return element.GetValue(SourceUrlProperty);
    }

    private static async Task LoadAsync(Image image, string? url)
    {
        image.Source = null;
        if (string.IsNullOrWhiteSpace(url)) return;

        if (Cache.TryGetValue(url, out var cached))
        {
            image.Source = cached;
            return;
        }

        try
        {
            var diskPath = DiskPath(url);

            byte[] bytes;
            if (File.Exists(diskPath))
            {
                bytes = await File.ReadAllBytesAsync(diskPath);
            }
            else
            {
                bytes = await HttpClientProvider.Shared.GetByteArrayAsync(url);
                Directory.CreateDirectory(Path.GetDirectoryName(diskPath)!);
                await File.WriteAllBytesAsync(diskPath, bytes);
            }

            using var stream = new MemoryStream(bytes);
            var bitmap = new Bitmap(stream);
            Cache[url] = bitmap;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (GetSourceUrl(image) == url) image.Source = bitmap;
            });
        }
        catch
        {
            // ignora imagens que falham a carregar
        }
    }

    private static string DiskPath(string url)
    {
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(url)));
        return Path.Combine(LauncherPaths.ImageCacheDir, hash + ".img");
    }
}